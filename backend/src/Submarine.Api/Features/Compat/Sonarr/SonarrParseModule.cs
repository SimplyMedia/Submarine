using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Modules;
using Submarine.Core.Parser;
using Submarine.Core.Release.Torrent;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;

namespace Submarine.Api.Features.Compat.Sonarr;

/// <summary>
///     Sonarr-shaped release title parsing over the frozen native torrent release parser, matched against the
///     library the same way an interactive search would.
/// </summary>
public sealed class SonarrParseModule : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, "sonarr", "v3");
		group.MapGet("/parse", ParseAsync);
	}

	private static async Task<IResult> ParseAsync(
		string title, IParser<TorrentRelease> parser, ReleaseMatcher matcher, SubmarineDbContext db, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(title)) return CompatErrors.Validation("title", "title is required");

		TorrentRelease parsed;
		try
		{
			parsed = parser.Parse(title);
		}
		catch (Exception)
		{
			return Results.Json(new { title, parsedEpisodeInfo = (object?)null, series = (object?)null, episodes = Array.Empty<object>() }, CompatJson.Options);
		}

		var match = await matcher.MatchLibraryAsync(parsed, ct);
		var series = match?.SeriesId is { } seriesId ? await db.Series.AsNoTracking().FirstOrDefaultAsync(x => x.Id == seriesId, ct) : null;
		var episodes = match?.EpisodeIds is { Count: > 0 } episodeIds
			? await db.Episodes.AsNoTracking().Where(x => episodeIds.Contains(x.Id)).ToListAsync(ct)
			: [];

		return Results.Json(new
		{
			title,
			parsedEpisodeInfo = new
			{
				releaseTitle = parsed.FullTitle,
				seriesTitle = parsed.Title,
				seasonNumber = parsed.SeriesReleaseData?.Seasons.FirstOrDefault() ?? 0,
				episodeNumbers = parsed.SeriesReleaseData?.Episodes ?? [],
				absoluteEpisodeNumbers = parsed.SeriesReleaseData?.AbsoluteEpisodes ?? [],
				quality = new { quality = new { id = (int)(parsed.Quality.Resolution.Source ?? default), name = parsed.Quality.Resolution.Name }, revision = parsed.Quality.Revision },
				releaseGroup = parsed.ReleaseGroup,
				isDaily = false,
				isAbsoluteNumbering = parsed.SeriesReleaseData?.AbsoluteEpisodes.Count > 0
			},
			series = series is null ? null : new { id = series.Id, title = series.Title, year = series.Year, tvdbId = series.TvdbId },
			episodes = episodes.Select(x => new { id = x.Id, seasonNumber = x.SeasonNumber, episodeNumber = x.EpisodeNumber, title = x.Title })
		}, CompatJson.Options);
	}
}
