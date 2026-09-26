using Submarine.Core.Indexers;

namespace Submarine.Core.Search;

/// <summary>
///     Builds indexer search requests for library items. Season and episode numbers are expected to already be
///     resolved to the numbering the indexer is searched with (scene numbering when applicable).
/// </summary>
public static class SearchRequestBuilder
{
	/// <summary>Builds a request covering every episode of a series.</summary>
	public static TvSearchRequest BuildSeriesQuery(
		string title, int? tvdbId, int? tmdbId, string? imdbId, IReadOnlyList<int>? categories = null, bool isRss = false)
		=> new(Query: title, TvdbId: tvdbId, TmdbId: tmdbId, ImdbId: imdbId, Categories: categories, IsRss: isRss);

	/// <summary>Builds a request for a full season of a series.</summary>
	public static TvSearchRequest BuildSeasonQuery(
		string title, int seasonNumber, int? tvdbId, int? tmdbId, string? imdbId, IReadOnlyList<int>? categories = null)
		=> new(
			Query: $"{title} S{seasonNumber:00}",
			Season: seasonNumber,
			TvdbId: tvdbId,
			TmdbId: tmdbId,
			ImdbId: imdbId,
			Categories: categories);

	/// <summary>Builds a request for a standard aired episode.</summary>
	public static TvSearchRequest BuildStandardEpisodeQuery(
		string title, int seasonNumber, int episodeNumber, int? tvdbId, int? tmdbId, string? imdbId,
		IReadOnlyList<int>? categories = null)
		=> new(
			Query: $"{title} S{seasonNumber:00}E{episodeNumber:00}",
			Season: seasonNumber,
			Episode: episodeNumber,
			TvdbId: tvdbId,
			TmdbId: tmdbId,
			ImdbId: imdbId,
			Categories: categories);

	/// <summary>Builds a request for a daily (talk show) episode aired on the given date.</summary>
	public static TvSearchRequest BuildDailyEpisodeQuery(
		string title, DateTime airDate, int? tvdbId, int? tmdbId, string? imdbId, IReadOnlyList<int>? categories = null)
		=> new(
			Query: $"{title} {airDate:yyyy.MM.dd}",
			TvdbId: tvdbId,
			TmdbId: tmdbId,
			ImdbId: imdbId,
			Categories: categories);

	/// <summary>
	///     Builds the anime search requests for one episode: an absolute-numbered query always, and additionally a
	///     standard SxxEyy query when the series also wants aired-order releases searched.
	/// </summary>
	public static IReadOnlyList<TvSearchRequest> BuildAnimeEpisodeQueries(
		string title, int seasonNumber, int episodeNumber, int absoluteEpisodeNumber, int? tvdbId, int? tmdbId,
		string? imdbId, bool animeStandardFormatSearch, IReadOnlyList<int>? categories = null)
	{
		var requests = new List<TvSearchRequest>
		{
			new(
				Query: $"{title} {absoluteEpisodeNumber:000}",
				AbsoluteEpisode: absoluteEpisodeNumber,
				TvdbId: tvdbId,
				TmdbId: tmdbId,
				ImdbId: imdbId,
				Categories: categories)
		};

		if (animeStandardFormatSearch)
		{
			requests.Add(BuildStandardEpisodeQuery(title, seasonNumber, episodeNumber, tvdbId, tmdbId, imdbId, categories));
		}

		return requests;
	}

	/// <summary>Builds a request for a movie.</summary>
	public static MovieSearchRequest BuildMovieQuery(
		string title, int? year, string? imdbId, int? tmdbId, IReadOnlyList<int>? categories = null)
		=> new(Query: title, Year: year, ImdbId: imdbId, TmdbId: tmdbId, Categories: categories);

	/// <summary>Builds a free text search request.</summary>
	public static BasicSearchRequest BuildTextQuery(string term, IReadOnlyList<int>? categories = null)
		=> new(Query: term, Categories: categories);
}
