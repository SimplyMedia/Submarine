using System.Collections.Generic;

namespace Submarine.Core.Indexers;

/// <summary>
///     A search against an indexer
/// </summary>
/// <param name="Query">The free text query, if any</param>
/// <param name="Categories">The standard category ids to search in, if any</param>
/// <param name="Limit">The maximum amount of results, if any</param>
/// <param name="Offset">The offset into the results, if any</param>
/// <param name="IsRss">Whether this is an RSS sync fetch rather than a user search</param>
public abstract record SearchRequest(
	string? Query,
	IReadOnlyList<int> Categories,
	int? Limit,
	int? Offset,
	bool IsRss);

/// <summary>
///     A basic free text search
/// </summary>
/// <param name="Query">The free text query, if any</param>
/// <param name="Categories">The standard category ids to search in, if any</param>
/// <param name="Limit">The maximum amount of results, if any</param>
/// <param name="Offset">The offset into the results, if any</param>
/// <param name="IsRss">Whether this is an RSS sync fetch rather than a user search</param>
public record BasicSearchRequest(
	string? Query = null,
	IReadOnlyList<int>? Categories = null,
	int? Limit = null,
	int? Offset = null,
	bool IsRss = false) : SearchRequest(Query, Categories ?? [], Limit, Offset, IsRss);

/// <summary>
///     A tv search
/// </summary>
/// <param name="Query">The free text query, if any</param>
/// <param name="Season">The season number, if any</param>
/// <param name="Episode">The episode number, if any</param>
/// <param name="TvdbId">The TVDB id, if any</param>
/// <param name="TmdbId">The TMDB id, if any</param>
/// <param name="ImdbId">The IMDb id with tt prefix, if any</param>
/// <param name="AbsoluteEpisode">The absolute episode number, if any</param>
/// <param name="Categories">The standard category ids to search in, if any</param>
/// <param name="Limit">The maximum amount of results, if any</param>
/// <param name="Offset">The offset into the results, if any</param>
/// <param name="IsRss">Whether this is an RSS sync fetch rather than a user search</param>
public record TvSearchRequest(
	string? Query = null,
	int? Season = null,
	int? Episode = null,
	int? TvdbId = null,
	int? TmdbId = null,
	string? ImdbId = null,
	int? AbsoluteEpisode = null,
	IReadOnlyList<int>? Categories = null,
	int? Limit = null,
	int? Offset = null,
	bool IsRss = false) : SearchRequest(Query, Categories ?? [], Limit, Offset, IsRss);

/// <summary>
///     A movie search
/// </summary>
/// <param name="Query">The free text query, if any</param>
/// <param name="Year">The release year, if any</param>
/// <param name="ImdbId">The IMDb id with tt prefix, if any</param>
/// <param name="TmdbId">The TMDB id, if any</param>
/// <param name="Categories">The standard category ids to search in, if any</param>
/// <param name="Limit">The maximum amount of results, if any</param>
/// <param name="Offset">The offset into the results, if any</param>
/// <param name="IsRss">Whether this is an RSS sync fetch rather than a user search</param>
public record MovieSearchRequest(
	string? Query = null,
	int? Year = null,
	string? ImdbId = null,
	int? TmdbId = null,
	IReadOnlyList<int>? Categories = null,
	int? Limit = null,
	int? Offset = null,
	bool IsRss = false) : SearchRequest(Query, Categories ?? [], Limit, Offset, IsRss);
