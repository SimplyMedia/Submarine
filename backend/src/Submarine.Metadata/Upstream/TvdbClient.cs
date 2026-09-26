using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Options;

namespace Submarine.Metadata.Upstream;

/// <summary>
///     TVDB v4 access. Logs in with the project API key, caches the bearer
///     token for 28 days (their token lifetime), serialises logins through a
///     semaphore and retries a request once after a 401 forced re-login.
/// </summary>
public sealed class TvdbClient(IHttpClientFactory httpClientFactory, HybridCache cache, IOptions<TvdbOptions> options)
{
	public const string ClientName = "tvdb";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly HybridCacheEntryOptions TokenCacheOptions = new() { Expiration = TimeSpan.FromDays(28) };
	private const string TokenCacheKey = "tvdb:token";
	private const int MaxEpisodePages = 50;
	private readonly SemaphoreSlim _loginLock = new(1, 1);

	public async Task<SeriesResource?> GetSeriesByTvdbAsync(int tvdbId, CancellationToken cancellationToken = default)
	{
		var envelope = await GetJsonAsync<TvdbEnvelope<TvdbSeriesDetail>>(
			$"series/{tvdbId}/extended?meta=episodes&short=true", cancellationToken);
		var detail = envelope?.Data;
		if (detail is null) return null;

		var defaultEpisodes = await GetEpisodesAsync(tvdbId, "default", cancellationToken);
		var dvdEpisodes = await GetEpisodesAsync(tvdbId, "dvd", cancellationToken);
		var absoluteEpisodes = await GetEpisodesAsync(tvdbId, "absolute", cancellationToken);

		var translations = (await GetJsonAsync<TvdbEnvelope<TvdbTranslations>>($"series/{tvdbId}/translations", cancellationToken))?.Data;
		return MapSeries(detail, defaultEpisodes, dvdEpisodes, absoluteEpisodes, translations);
	}

	public async Task<IReadOnlyList<SearchResultResource>> SearchSeriesAsync(string term, CancellationToken cancellationToken = default)
	{
		var search = await GetJsonAsync<TvdbEnvelope<IReadOnlyList<TvdbSearchItem>>>(
			$"search?query={Uri.EscapeDataString(term)}&type=series", cancellationToken);
		return (search?.Data ?? []).Select(MapSearchResult).ToList();
	}

	private async Task<List<TvdbEpisodeEntry>> GetEpisodesAsync(int tvdbId, string seasonType, CancellationToken cancellationToken)
	{
		var episodes = new List<TvdbEpisodeEntry>();
		for (var page = 0; page < MaxEpisodePages; page++)
		{
			var envelope = await GetJsonAsync<TvdbEnvelope<TvdbEpisodesPage>>(
				$"series/{tvdbId}/episodes/{seasonType}?page={page}", cancellationToken);
			var result = envelope?.Data;
			if (result?.Episodes is not { Count: > 0 } batch) break;
			episodes.AddRange(batch);
			if (string.IsNullOrEmpty(result.Links?.Next)) break;
		}

		return episodes;
	}

	private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken) where T : class
	{
		var response = await SendAsync(HttpMethod.Get, path, cancellationToken);
		if (response.StatusCode == HttpStatusCode.Unauthorized)
		{
			response.Dispose();
			await InvalidateTokenAsync(cancellationToken);
			response = await SendAsync(HttpMethod.Get, path, cancellationToken);
		}

		using (response)
		{
			if (response.StatusCode == HttpStatusCode.NotFound) return null;
			if (!response.IsSuccessStatusCode)
				throw new UpstreamException(
					"tvdb",
					(int)response.StatusCode,
					$"TVDB request to '{path}' failed with status {(int)response.StatusCode}.");
			return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
		}
	}

	private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, CancellationToken cancellationToken)
	{
		var client = httpClientFactory.CreateClient(ClientName);
		using var request = new HttpRequestMessage(method, path);
		var token = await GetTokenAsync(cancellationToken);
		if (!string.IsNullOrEmpty(token))
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return await client.SendAsync(request, cancellationToken);
	}

	private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
	{
		await _loginLock.WaitAsync(cancellationToken);
		try
		{
			return await cache.GetOrCreateAsync(
				TokenCacheKey,
				this,
				static async (self, token) => await self.LoginAsync(token),
				TokenCacheOptions,
				cancellationToken: cancellationToken);
		}
		finally
		{
			_loginLock.Release();
		}
	}

	private async Task<string> LoginAsync(CancellationToken cancellationToken)
	{
		var client = httpClientFactory.CreateClient(ClientName);
		using var response = await client.PostAsJsonAsync(
			"login",
			new TvdbLoginRequest(options.Value.ApiKey),
			JsonOptions,
			cancellationToken);
		if (!response.IsSuccessStatusCode)
			throw new UpstreamException(
				"tvdb",
				(int)response.StatusCode,
				$"TVDB login failed with status {(int)response.StatusCode}.");
		var payload = await response.Content.ReadFromJsonAsync<TvdbEnvelope<TvdbLoginData>>(JsonOptions, cancellationToken);
		return payload?.Data?.Token
			?? throw new UpstreamException("tvdb", (int)response.StatusCode, "TVDB login returned no token.");
	}

	private async Task InvalidateTokenAsync(CancellationToken cancellationToken)
	{
		await _loginLock.WaitAsync(cancellationToken);
		try
		{
			await cache.RemoveAsync(TokenCacheKey, cancellationToken);
		}
		finally
		{
			_loginLock.Release();
		}
	}

	internal static SeriesResource MapSeries(
		TvdbSeriesDetail detail,
		IReadOnlyList<TvdbEpisodeEntry> defaultEpisodes,
		IReadOnlyList<TvdbEpisodeEntry> dvdEpisodes,
		IReadOnlyList<TvdbEpisodeEntry> absoluteEpisodes,
		TvdbTranslations? translations)
	{
		var aired = defaultEpisodes
			.Where(e => e.SeasonNumber is not null && e.Number is not null)
			.ToList();
		var allIds = aired.Select(e => e.Id).ToHashSet();

		// DVD ordering is only useful when it numbers every episode; partial
		// DVD lists would leave later episodes unmatchable.
		var dvd = dvdEpisodes
			.Where(e => e.SeasonNumber is not null && e.Number is not null)
			.GroupBy(e => e.Id)
			.ToDictionary(g => g.Key, g => (g.First().SeasonNumber!.Value, g.First().Number!.Value));
		var dvdComplete = dvd.Count > 0 && dvd.Keys.ToHashSet().SetEquals(allIds);

		// Absolute entries with number 0 are TVDB placeholders, not positions.
		var absolute = absoluteEpisodes
			.Select(e => (e.Id, Absolute: e.AbsoluteNumber ?? e.Number))
			.Where(x => x.Absolute is > 0)
			.GroupBy(x => x.Id)
			.ToDictionary(g => g.Key, g => g.First().Absolute!.Value);

		var episodes = aired
			.OrderBy(e => e.SeasonNumber)
			.ThenBy(e => e.Number)
			.Select(e =>
			{
				var numbers = new List<EpisodeNumber> { new(EpisodeOrdering.AIRED, e.SeasonNumber, e.Number, null) };
				if (dvdComplete && dvd.TryGetValue(e.Id, out var dvdPosition))
					numbers.Add(new EpisodeNumber(EpisodeOrdering.DVD, dvdPosition.Item1, dvdPosition.Item2, null));
				if (absolute.TryGetValue(e.Id, out var absoluteNumber))
					numbers.Add(new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, absoluteNumber));
				return new EpisodeResource(
					e.Id,
					null,
					e.Name,
					e.Overview,
					MetadataMapping.ParseDate(e.Aired),
					null,
					e.Runtime,
					numbers,
					e.Image);
			})
			.ToList();

		var seasons = (detail.Seasons ?? [])
			.Where(s => s.Type?.Name?.Equals("default", StringComparison.OrdinalIgnoreCase) == true)
			.OrderBy(s => s.SeasonNumber)
			.Select(s => new SeasonResource(
				s.SeasonNumber,
				null,
				s.EpisodeCount ?? aired.Count(e => e.SeasonNumber == s.SeasonNumber)))
			.ToList();

		var alternateTitles = (detail.Aliases ?? [])
			.Where(a => !string.IsNullOrWhiteSpace(a.Alias) && !a.Alias!.Equals(detail.Name, StringComparison.OrdinalIgnoreCase))
			.Select(a => new AlternateTitleResource(a.Alias!.Trim(), a.Language))
			.Concat((translations?.NameTranslations ?? [])
				.Where(t => !string.IsNullOrWhiteSpace(t.Name) && !t.Name!.Equals(detail.Name, StringComparison.OrdinalIgnoreCase))
				.Select(t => new AlternateTitleResource(t.Name!.Trim(), t.Language)))
			.Distinct()
			.ToList();

		var network = (detail.Companies ?? [])
			.FirstOrDefault(c => c.Type?.Name?.Equals("Network", StringComparison.OrdinalIgnoreCase) == true)?.Name;

		return new SeriesResource(
			detail.Id,
			RemoteIdNumber(detail, "TMDB"),
			RemoteIdString(detail, "IMDB"),
			detail.Name ?? string.Empty,
			MetadataMapping.ToSortTitle(detail.Name),
			detail.Overview,
			MetadataMapping.ParseDate(detail.FirstAired),
			MetadataMapping.MapTvdbStatus(detail.Status?.Name),
			detail.AverageRuntime,
			network,
			(detail.Genres ?? []).Select(g => g.Name ?? string.Empty).Where(n => n.Length > 0).ToList(),
			null,
			seasons,
			episodes,
			detail.Poster,
			detail.Backdrops?.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)),
			MetadataMapping.ParseDate(detail.FirstAired)?.Year,
			alternateTitles);
	}

	internal static SearchResultResource MapSearchResult(TvdbSearchItem item) => new(
		item.TvdbId is > 0 ? item.TvdbId : null,
		null,
		null,
		item.Name ?? string.Empty,
		int.TryParse(item.Year, out var year) ? year : null,
		item.Overview,
		item.ImageUrl,
		item.Status?.Name,
		"tvdb");

	private static int? RemoteIdNumber(TvdbSeriesDetail detail, string typeName) =>
		int.TryParse(RemoteIdRaw(detail, typeName), out var value) ? value : null;

	private static string? RemoteIdString(TvdbSeriesDetail detail, string typeName) =>
		RemoteIdRaw(detail, typeName) is { Length: > 0 } value ? value : null;

	private static string? RemoteIdRaw(TvdbSeriesDetail detail, string typeName)
	{
		var match = (detail.RemoteIds ?? [])
			.FirstOrDefault(r => r.Type?.Name?.Equals(typeName, StringComparison.OrdinalIgnoreCase) == true);
		return match is null ? null : match.Id.ToString();
	}
}
