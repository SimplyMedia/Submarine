using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Profiles;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>Searches every episode of a series.</summary>
public sealed class SeriesSearchCommandHandler(AutomaticSearchService automaticSearchService) : ICommandHandler<SeriesSearchCommand>
{
	/// <inheritdoc />
	public Task ExecuteAsync(SeriesSearchCommand command, ICommandContext context, CancellationToken cancellationToken = default)
		=> automaticSearchService.SearchSeriesAsync(command.SeriesId, cancellationToken);
}

/// <summary>Searches one season of a series.</summary>
public sealed class SeasonSearchCommandHandler(AutomaticSearchService automaticSearchService) : ICommandHandler<SeasonSearchCommand>
{
	/// <inheritdoc />
	public Task ExecuteAsync(SeasonSearchCommand command, ICommandContext context, CancellationToken cancellationToken = default)
		=> automaticSearchService.SearchSeasonAsync(command.SeriesId, command.SeasonNumber, cancellationToken);
}

/// <summary>Searches a specific set of episodes.</summary>
public sealed class EpisodeSearchCommandHandler(AutomaticSearchService automaticSearchService) : ICommandHandler<EpisodeSearchCommand>
{
	/// <inheritdoc />
	public Task ExecuteAsync(EpisodeSearchCommand command, ICommandContext context, CancellationToken cancellationToken = default)
		=> automaticSearchService.SearchEpisodesAsync(command.EpisodeIds, cancellationToken);
}

/// <summary>Searches a specific set of movies.</summary>
public sealed class MovieSearchCommandHandler(AutomaticSearchService automaticSearchService) : ICommandHandler<MovieSearchCommand>
{
	/// <inheritdoc />
	public Task ExecuteAsync(MovieSearchCommand command, ICommandContext context, CancellationToken cancellationToken = default)
		=> automaticSearchService.SearchMoviesAsync(command.MovieIds, cancellationToken);
}

/// <summary>
///     Searches every monitored, missing (aired, no file) episode and movie, oldest last-searched first.
/// </summary>
public sealed class MissingSearchCommandHandler(SubmarineDbContext db, AutomaticSearchService automaticSearchService)
	: ICommandHandler<MissingSearchCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(MissingSearchCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;

		var episodeIds = await db.Episodes.AsNoTracking()
			.Where(episode => episode.Monitored
				&& episode.Series.Monitored
				&& episode.AirDateUtc != null && episode.AirDateUtc <= now
				&& episode.Files.Count == 0)
			.OrderBy(episode => episode.LastSearchTime ?? DateTime.MinValue)
			.Select(episode => episode.Id)
			.ToListAsync(cancellationToken);

		var movieIds = await db.Movies.AsNoTracking()
			.Where(movie => movie.Monitored && movie.Files.Count == 0)
			.OrderBy(movie => movie.LastSearchTime ?? DateTime.MinValue)
			.Select(movie => movie.Id)
			.ToListAsync(cancellationToken);

		var total = episodeIds.Count + movieIds.Count;
		var done = 0;

		foreach (var episodeId in episodeIds)
		{
			await automaticSearchService.SearchEpisodesAsync([episodeId], cancellationToken);
			done++;
			await context.ReportProgressAsync(total == 0 ? 100 : done * 100 / total, cancellationToken: cancellationToken);
		}

		foreach (var movieId in movieIds)
		{
			await automaticSearchService.SearchMoviesAsync([movieId], cancellationToken);
			done++;
			await context.ReportProgressAsync(total == 0 ? 100 : done * 100 / total, cancellationToken: cancellationToken);
		}
	}
}

/// <summary>
///     Searches every media version whose held file does not meet the quality profile cutoff and upgrades are
///     allowed.
/// </summary>
public sealed class CutoffUnmetSearchCommandHandler(SubmarineDbContext db, AutomaticSearchService automaticSearchService)
	: ICommandHandler<CutoffUnmetSearchCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(CutoffUnmetSearchCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var profiles = await db.QualityProfiles.AsNoTracking().ToDictionaryAsync(profile => profile.Id, cancellationToken);

		var seriesIds = new HashSet<int>();
		foreach (var version in await db.MediaVersions.AsNoTracking().Where(version => version.SeriesId != null).ToListAsync(cancellationToken))
		{
			if (!profiles.TryGetValue(version.QualityProfileId, out var profile) || !profile.UpgradeAllowed)
			{
				continue;
			}

			var files = await db.EpisodeFiles.AsNoTracking()
				.Where(file => file.MediaVersionId == version.Id)
				.ToListAsync(cancellationToken);
			var unmet = files.Count > 0 && files.Any(file => !profile.MeetsCutoff(file.Quality));

			if (unmet)
			{
				seriesIds.Add(version.SeriesId!.Value);
			}
		}

		var movieIds = new List<int>();
		foreach (var version in await db.MediaVersions.AsNoTracking().Where(version => version.MovieId != null).ToListAsync(cancellationToken))
		{
			if (!profiles.TryGetValue(version.QualityProfileId, out var profile) || !profile.UpgradeAllowed)
			{
				continue;
			}

			var file = await db.MovieFiles.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.MediaVersionId == version.Id, cancellationToken);
			if (file is not null && !profile.MeetsCutoff(file.Quality))
			{
				movieIds.Add(version.MovieId!.Value);
			}
		}

		var total = seriesIds.Count + movieIds.Count;
		var done = 0;

		foreach (var seriesId in seriesIds)
		{
			await automaticSearchService.SearchSeriesAsync(seriesId, cancellationToken);
			done++;
			await context.ReportProgressAsync(total == 0 ? 100 : done * 100 / total, cancellationToken: cancellationToken);
		}

		if (movieIds.Count > 0)
		{
			await automaticSearchService.SearchMoviesAsync(movieIds, cancellationToken);
		}
	}
}
