using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Parser;
using Submarine.Core.Release;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Parse;

/// <summary>
///     Parses a release title and reports the matched library item, if any.
/// </summary>
public sealed class ParseModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/parse");
		group.MapGet("/", ParseAsync);
	}

	private static async Task<Results<Ok<ParseResult>, ProblemHttpResult>> ParseAsync(
		string title,
		SubmarineDbContext db,
		IParser<BaseRelease> parser,
		CancellationToken cancellationToken)
	{
		BaseRelease parsed;

		try
		{
			parsed = parser.Parse(title);
		}
		catch (Exception)
		{
			return TypedResults.Problem($"The title '{title}' could not be parsed as a release", statusCode: 400);
		}

		var cleanTitle = Clean(parsed.Title);

		// cleaned titles are stored by the library slice; match on the normalized form so the exact
		// storage convention (spaces, separators) does not matter
		Core.Entities.Series? series = null;
		Core.Entities.Episode[] episodes = [];

		if (cleanTitle.Length > 0)
		{
			var seriesCandidates = await db.Series
				.AsNoTracking()
				.Select(entry => new { entry.Id, entry.CleanTitle })
				.ToListAsync(cancellationToken);
			var seriesId = seriesCandidates.FirstOrDefault(entry => Clean(entry.CleanTitle) == cleanTitle)?.Id;

			if (seriesId is { } matchedSeriesId && parsed.SeriesReleaseData is { } seriesData)
			{
				series = await db.Series.FirstAsync(entry => entry.Id == matchedSeriesId, cancellationToken);
				episodes = await db.Episodes
					.Where(episode => episode.SeriesId == matchedSeriesId)
					.Where(episode => seriesData.Seasons.Contains(episode.SeasonNumber))
					.Where(episode => seriesData.Episodes.Count == 0
					                  || seriesData.Episodes.Contains(episode.EpisodeNumber)
					                  || seriesData.AbsoluteEpisodes.Contains(episode.AbsoluteEpisodeNumber ?? -1))
					.ToArrayAsync(cancellationToken);
			}
		}

		Core.Entities.Movie? movie = null;

		if (cleanTitle.Length > 0)
		{
			var movieCandidates = await db.Movies
				.AsNoTracking()
				.Select(entry => new { entry.Id, entry.CleanTitle })
				.ToListAsync(cancellationToken);
			var movieId = movieCandidates.FirstOrDefault(entry => Clean(entry.CleanTitle) == cleanTitle)?.Id;

			if (movieId is { } matchedMovieId)
			{
				movie = await db.Movies.FirstAsync(entry => entry.Id == matchedMovieId, cancellationToken);
			}
		}

		return TypedResults.Ok(new ParseResult(
			ParsedReleaseResource.From(parsed),
			series is null && movie is null
				? null
				: new ParseMatch(series?.Id, movie?.Id, [.. episodes.Select(episode => episode.Id)])));
	}

	// normalized form of a title: lowercase alphanumerics only
	private static string Clean(string title)
		=> new(title.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

/// <summary>The result of parsing a release title.</summary>
/// <param name="Parsed">The parsed release.</param>
/// <param name="Match">The matched library item, null when the release does not belong to the library yet.</param>
public sealed record ParseResult(ParsedReleaseResource Parsed, ParseMatch? Match);

/// <summary>The library items a parsed release matched.</summary>
/// <param name="SeriesId">Id of the matched series, if any.</param>
/// <param name="MovieId">Id of the matched movie, if any.</param>
/// <param name="EpisodeIds">Ids of the matched episodes.</param>
public sealed record ParseMatch(int? SeriesId, int? MovieId, IReadOnlyList<int> EpisodeIds);
