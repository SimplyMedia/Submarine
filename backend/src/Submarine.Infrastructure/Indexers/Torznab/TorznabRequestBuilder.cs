using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers.Torznab;

/// <summary>
///     Builds Torznab and Newznab api query strings
/// </summary>
/// <param name="apiKey">The api key, omitted when null or empty</param>
public sealed class TorznabRequestBuilder(string? apiKey)
{
	/// <summary>
	///     Builds a capabilities query (t=caps)
	/// </summary>
	/// <returns>The relative query string</returns>
	public string BuildCapsQuery()
		=> BuildQuery("caps");

	/// <summary>
	///     Builds the query of a search request
	/// </summary>
	/// <param name="request">The search request</param>
	/// <param name="additionalParameters">Raw extra query parameters, if any</param>
	/// <returns>The relative query string</returns>
	public string BuildSearchQuery(SearchRequest request, string? additionalParameters = null)
		=> request switch
		{
			TvSearchRequest tv => BuildQuery(
				"tvsearch",
				("q", Escape(tv.Query)),
				("season", tv.Season?.ToString()),
				("ep", tv.Episode?.ToString()),
				("tvdbid", tv.TvdbId?.ToString()),
				("tmdbid", tv.TmdbId?.ToString()),
				("imdbid", Escape(StripTt(tv.ImdbId))),
				("cat", Categories(tv.Categories)),
				("limit", request.Limit?.ToString()),
				("offset", request.Offset?.ToString()),
				AdditionalParameters(additionalParameters)),
			MovieSearchRequest movie => BuildQuery(
				"movie",
				("q", Escape(movie.Query)),
				("imdbid", Escape(StripTt(movie.ImdbId))),
				("tmdbid", movie.TmdbId?.ToString()),
				("year", movie.Year?.ToString()),
				("cat", Categories(request.Categories)),
				("limit", request.Limit?.ToString()),
				("offset", request.Offset?.ToString()),
				AdditionalParameters(additionalParameters)),
			_ => BuildQuery(
				"search",
				("q", Escape(request.Query)),
				("cat", Categories(request.Categories)),
				("limit", request.Limit?.ToString()),
				("offset", request.Offset?.ToString()),
				AdditionalParameters(additionalParameters))
		};

	/// <summary>
	///     Combines a base url, api path and relative query into the request uri
	/// </summary>
	/// <param name="baseUrl">The base url of the indexer</param>
	/// <param name="apiPath">The api path relative to the base url</param>
	/// <param name="query">The relative query string</param>
	/// <returns>The full request uri</returns>
	public static Uri ToUri(string baseUrl, string apiPath, string query)
	{
		var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
		var path = apiPath.TrimStart('/');
		return new Uri(baseUri, path + query);
	}

	private static (string Name, string? Value) AdditionalParameters(string? parameters)
		=> ("__raw", parameters);

	private static string? Categories(IReadOnlyList<int> categories)
		=> categories is { Count: > 0 } ? string.Join(",", categories) : null;

	private static string? StripTt(string? imdbId)
		=> imdbId?.StartsWith("tt", StringComparison.OrdinalIgnoreCase) == true ? imdbId[2..] : imdbId;

	private static string? Escape(string? value)
		=> value is null ? null : Uri.EscapeDataString(value);

	private string BuildQuery(string type, params (string Name, string? Value)[] parameters)
	{
		var builder = new StringBuilder("?t=").Append(type);

		if (!string.IsNullOrEmpty(apiKey))
			builder.Append("&apikey=").Append(Uri.EscapeDataString(apiKey));

		foreach (var (name, value) in parameters)
		{
			if (value is null)
				continue;
			if (name == "__raw")
			{
				builder.Append(value.StartsWith('&') ? value : "&" + value);
				continue;
			}

			builder.Append('&').Append(name).Append('=').Append(value);
		}

		return builder.ToString();
	}
}
