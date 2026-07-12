using System;
using System.Collections.Generic;
using System.Text;

namespace Submarine.Core.Indexer.Torznab;

/// <summary>
///     Builds Torznab/Newznab API query strings
/// </summary>
public class TorznabRequestBuilder
{
	private readonly string? _apiKey;

	/// <summary>
	///     Creates a new <see cref="TorznabRequestBuilder" />
	/// </summary>
	/// <param name="apiKey">The api key of the indexer, omitted from queries when null</param>
	public TorznabRequestBuilder(string? apiKey)
		=> _apiKey = apiKey;

	/// <summary>
	///     Builds a capabilities (t=caps) query
	/// </summary>
	/// <returns>The relative query string</returns>
	public string BuildCapsQuery()
		=> BuildQuery("caps");

	/// <summary>
	///     Builds a free text search (t=search) query
	/// </summary>
	/// <param name="query">The search term, if any</param>
	/// <param name="categories">The category ids to search in, if any</param>
	/// <param name="limit">The maximum amount of results, if any</param>
	/// <param name="offset">The offset into the results, if any</param>
	/// <returns>The relative query string</returns>
	public string BuildSearchQuery(string? query = null, IReadOnlyList<int>? categories = null, int? limit = null,
		int? offset = null)
		=> BuildQuery("search",
			("q", Escape(query)),
			("cat", categories is { Count: > 0 } ? string.Join(",", categories) : null),
			("limit", limit?.ToString()),
			("offset", offset?.ToString()));

	/// <summary>
	///     Builds a tv search (t=tvsearch) query
	/// </summary>
	/// <param name="query">The search term, if any</param>
	/// <param name="season">The season number, if any</param>
	/// <param name="episode">The episode number, if any</param>
	/// <param name="tvdbId">The TVDB id, if any</param>
	/// <param name="imdbId">The IMDb id, if any</param>
	/// <param name="rid">The TVRage id, if any</param>
	/// <param name="categories">The category ids to search in, if any</param>
	/// <returns>The relative query string</returns>
	public string BuildTvSearchQuery(string? query = null, int? season = null, int? episode = null,
		int? tvdbId = null, string? imdbId = null, int? rid = null, IReadOnlyList<int>? categories = null)
		=> BuildQuery("tvsearch",
			("q", Escape(query)),
			("season", season?.ToString()),
			("ep", episode?.ToString()),
			("tvdbid", tvdbId?.ToString()),
			("imdbid", Escape(imdbId)),
			("rid", rid?.ToString()),
			("cat", categories is { Count: > 0 } ? string.Join(",", categories) : null));

	/// <summary>
	///     Builds a movie search (t=movie) query
	/// </summary>
	/// <param name="query">The search term, if any</param>
	/// <param name="imdbId">The IMDb id, if any</param>
	/// <param name="tmdbId">The TMDB id, if any</param>
	/// <param name="categories">The category ids to search in, if any</param>
	/// <returns>The relative query string</returns>
	public string BuildMovieSearchQuery(string? query = null, string? imdbId = null, int? tmdbId = null,
		IReadOnlyList<int>? categories = null)
		=> BuildQuery("movie",
			("q", Escape(query)),
			("imdbid", Escape(imdbId)),
			("tmdbid", tmdbId?.ToString()),
			("cat", categories is { Count: > 0 } ? string.Join(",", categories) : null));

	/// <summary>
	///     Combines a base url with a relative query string
	/// </summary>
	/// <param name="baseUri">The base url of the indexer api endpoint</param>
	/// <param name="query">The relative query string</param>
	/// <returns>The full request Uri</returns>
	public Uri ToUri(Uri baseUri, string query)
		=> new(baseUri, query);

	private string BuildQuery(string type, params (string Name, string? Value)[] parameters)
	{
		var builder = new StringBuilder("?t=").Append(type);

		if (_apiKey != null)
			builder.Append("&apikey=").Append(Uri.EscapeDataString(_apiKey));

		foreach (var (name, value) in parameters)
		{
			if (value == null) continue;
			builder.Append('&').Append(name).Append('=').Append(value);
		}

		return builder.ToString();
	}

	private static string? Escape(string? value)
		=> value == null ? null : Uri.EscapeDataString(value);
}
