using Submarine.Infrastructure.Commands;
using Submarine.Core.Commands;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Library;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Refreshes series and movie metadata from the Metadata service and diffs
///     seasons, episodes and alternative titles.
/// </summary>
public sealed class LibraryRefresher(
	SubmarineDbContext db,
	IMetadataClient metadata,
	IEventBus eventBus,
	CollectionSyncService collectionSync,
	CollectionAddMissingService collectionAddMissing,
	NamingService namingService,
	ICommandQueue commandQueue,
	TimeProvider timeProvider)
{
	/// <summary>
	///     Refresh one series. Returns false when the provider has no metadata for it.
	/// </summary>
	public async Task<bool> RefreshSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
	{
		var series = await db.Series
			.Include(x => x.Seasons)
			.Include(x => x.Episodes)
			.Include(x => x.Tags)
			.FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {seriesId} not found");

		var resource = series.MetadataProvider == MetadataProvider.TMDB && series.TmdbId is { } tmdbId
			? await metadata.GetSeriesByTmdbAsync(tmdbId, cancellationToken)
			: await metadata.GetSeriesByTvdbAsync(series.TvdbId, cancellationToken);
		if (resource is null)
		{
			return false;
		}

		series.TmdbId = resource.TmdbId;
		series.ImdbId = resource.ImdbId;
		series.Title = resource.Title ?? series.Title;
		series.SortTitle = TitleNormalizer.SortTitle(series.Title);
		series.CleanTitle = TitleNormalizer.CleanTitle(series.Title);
		series.Overview = resource.Overview;
		series.Network = resource.Network;
		series.Runtime = resource.Runtime;
		series.Year = resource.Year ?? resource.FirstAired?.Year;
		series.PosterUrl = resource.PosterUrl;
		series.BackdropUrl = resource.BackdropUrl;
		series.Status = MapStatus(resource.Status);
		series.Genres = [.. resource.Genres];
		series.Certification = resource.Certification;
		series.FirstAired = resource.FirstAired.HasValue
			? DateTime.SpecifyKind(resource.FirstAired.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
			: null;
		series.LastRefreshedAt = timeProvider.GetUtcNow().UtcDateTime;

		var newSeasons = 0;
		foreach (var seasonResource in resource.Seasons)
		{
			if (series.Seasons.Any(x => x.SeasonNumber == seasonResource.SeasonNumber))
			{
				continue;
			}

			series.Seasons.Add(new Season
			{
				SeriesId = series.Id,
				SeasonNumber = seasonResource.SeasonNumber,
				Monitored = series.MonitorNewItems == MonitorNewItems.ALL
			});
			newSeasons++;
		}

		foreach (var episodeResource in resource.Episodes)
		{
			var existing = FindEpisode(series, episodeResource);
			if (existing is { } episode)
			{
				episode.Title = episodeResource.Title ?? episode.Title;
				episode.Overview = episodeResource.Overview ?? episode.Overview;
				episode.Runtime = episodeResource.Runtime ?? episode.Runtime;
				episode.TvdbId ??= episodeResource.TvdbId;
				episode.TmdbId ??= episodeResource.TmdbId;
				if (episodeResource.AirDate is { } airDate)
				{
					episode.AirDate = airDate.ToString("yyyy-MM-dd");
				}

				if (episodeResource.AirDateUtc is { } airDateUtc)
				{
					episode.AirDateUtc = DateTime.SpecifyKind(airDateUtc.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
				}

				continue;
			}

			if (PickNumbers(episodeResource, series.Numbering) is not { } numbers)
			{
				continue;
			}

			var (seasonNumber, episodeNumber, absolute) = numbers;
			var season = series.Seasons.FirstOrDefault(x => x.SeasonNumber == seasonNumber);
			if (season is null)
			{
				season = new Season
				{
					SeriesId = series.Id,
					SeasonNumber = seasonNumber,
					Monitored = series.MonitorNewItems == MonitorNewItems.ALL
				};
				series.Seasons.Add(season);
				newSeasons++;
			}

			series.Episodes.Add(new Episode
			{
				SeriesId = series.Id,
				SeasonNumber = seasonNumber,
				EpisodeNumber = episodeNumber,
				AbsoluteEpisodeNumber = absolute,
				TvdbId = episodeResource.TvdbId,
				TmdbId = episodeResource.TmdbId,
				Title = episodeResource.Title,
				Overview = episodeResource.Overview,
				AirDate = episodeResource.AirDate?.ToString("yyyy-MM-dd"),
				AirDateUtc = episodeResource.AirDateUtc.HasValue
					? DateTime.SpecifyKind(episodeResource.AirDateUtc.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
					: null,
				Runtime = episodeResource.Runtime,
				Monitored = season.Monitored
			});
		}

		var titles = await db.AlternativeTitles
			.Where(x => x.SeriesId == series.Id)
			.Select(x => x.Title)
			.ToListAsync(cancellationToken);
		foreach (var title in resource.AlternateTitles)
		{
			if (!string.IsNullOrWhiteSpace(title.Title)
				&& title.Title != series.Title
				&& !titles.Contains(title.Title))
			{
				db.AlternativeTitles.Add(new AlternativeTitle { SeriesId = series.Id, Title = title.Title });
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		await RenamePlaceholderFilesAsync(series, cancellationToken);
		await eventBus.PublishAsync(new SeriesUpdatedEvent(series.Id), cancellationToken);
		return true;
	}

	// Files imported before the provider knew the episode title were named "Episode 5"; rename them
	// once every episode they cover has a real title.
	private async Task RenamePlaceholderFilesAsync(Series series, CancellationToken cancellationToken)
	{
		var files = await db.EpisodeFiles
			.Include(file => file.Episodes)
			.Where(file => file.SeriesId == series.Id && file.NamedFromPlaceholder)
			.ToListAsync(cancellationToken);
		if (files.Count == 0)
		{
			return;
		}

		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var ready = files
			.Where(file => !namingService.UsesPlaceholderTitle(series, [.. file.Episodes], file, naming))
			.Select(file => file.Id)
			.ToList();
		if (ready.Count > 0)
		{
			await commandQueue.EnqueueAsync(new RenameSeriesCommand(series.Id, ready), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
		}
	}

	/// <summary>
	///     Refresh one movie. Returns false when the provider has no metadata for it.
	/// </summary>
	public async Task<bool> RefreshMovieAsync(int movieId, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {movieId} not found");

		var resource = await metadata.GetMovieAsync(movie.TmdbId, cancellationToken);
		if (resource is null)
		{
			return false;
		}

		movie.ImdbId = resource.ImdbId;
		movie.Title = resource.Title ?? movie.Title;
		movie.SortTitle = TitleNormalizer.SortTitle(movie.Title);
		movie.CleanTitle = TitleNormalizer.CleanTitle(movie.Title);
		movie.OriginalTitle = resource.OriginalTitle;
		movie.Overview = resource.Overview;
		movie.Year = resource.Year ?? movie.Year;
		movie.Runtime = resource.Runtime;
		movie.Studio = resource.Studio;
		movie.PosterUrl = resource.PosterUrl;
		movie.BackdropUrl = resource.BackdropUrl;
		movie.Status = MapStatus(resource.Status);
		movie.InCinemasDate = ToUtc(resource.InCinemasDate) ?? movie.InCinemasDate;
		movie.DigitalReleaseDate = ToUtc(resource.DigitalReleaseDate);
		movie.PhysicalReleaseDate = ToUtc(resource.PhysicalReleaseDate);
		movie.TmdbCollectionId = resource.TmdbCollectionId;
		movie.CollectionTitle = resource.CollectionTitle;
		movie.Genres = [.. resource.Genres];
		movie.Certification = resource.Certification;
		movie.YouTubeTrailerId = resource.YouTubeTrailerId;
		movie.LastRefreshedAt = timeProvider.GetUtcNow().UtcDateTime;

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new MovieUpdatedEvent(movie.Id), cancellationToken);

		var collection = await collectionSync.SyncAsync(movie, cancellationToken);
		if (collection is { Monitored: true })
		{
			try
			{
				await collectionAddMissing.AddMissingAsync(collection, cancellationToken);
			}
			catch (KeyNotFoundException)
			{
				// The metadata provider has no data for the collection right now; try again on the next refresh.
			}
		}

		return true;
	}

	/// <summary>
	///     Refresh all monitored series and movies. Reports progress over the combined count.
	/// </summary>
	public async Task<(int SeriesRefreshed, int MoviesRefreshed)> RefreshAllAsync(
		Func<int, int, Task>? progress,
		CancellationToken cancellationToken = default)
	{
		var seriesIds = await db.Series.Where(x => x.Monitored).Select(x => x.Id).ToListAsync(cancellationToken);
		var movieIds = await db.Movies.Where(x => x.Monitored).Select(x => x.Id).ToListAsync(cancellationToken);
		var total = seriesIds.Count + movieIds.Count;
		var done = 0;
		int seriesRefreshed = 0, moviesRefreshed = 0;

		foreach (var seriesId in seriesIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				if (await RefreshSeriesAsync(seriesId, cancellationToken))
				{
					seriesRefreshed++;
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				// One broken series must not stop the refresh of the rest.
			}

			done++;
			if (progress is not null)
			{
				await progress(done, total);
			}
		}

		foreach (var movieId in movieIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				if (await RefreshMovieAsync(movieId, cancellationToken))
				{
					moviesRefreshed++;
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				// One broken movie must not stop the refresh of the rest.
			}

			done++;
			if (progress is not null)
			{
				await progress(done, total);
			}
		}

		return (seriesRefreshed, moviesRefreshed);
	}

	private static Episode? FindEpisode(Series series, Contracts.Metadata.EpisodeResource resource)
	{
		if (resource.TvdbId is { } tvdbId)
		{
			var byTvdb = series.Episodes.FirstOrDefault(x => x.TvdbId == tvdbId);
			if (byTvdb is not null)
			{
				return byTvdb;
			}
		}

		if (resource.Numbers.FirstOrDefault(x => x.Ordering == Contracts.Metadata.EpisodeOrdering.AIRED)
			is { } aired
			&& aired.SeasonNumber is { } season
			&& aired.Number is { } number)
		{
			return series.Episodes.FirstOrDefault(x => x.SeasonNumber == season && x.EpisodeNumber == number);
		}

		return null;
	}

	private static (int Season, int Episode, int? Absolute)? PickNumbers(
		Contracts.Metadata.EpisodeResource resource,
		SeriesNumbering numbering)
	{
		var preferred = numbering switch
		{
			SeriesNumbering.DVD => Contracts.Metadata.EpisodeOrdering.DVD,
			SeriesNumbering.ABSOLUTE => Contracts.Metadata.EpisodeOrdering.ABSOLUTE,
			_ => Contracts.Metadata.EpisodeOrdering.AIRED
		};

		var chosen = resource.Numbers.FirstOrDefault(x => x.Ordering == preferred)
			?? resource.Numbers.FirstOrDefault(x => x.Ordering == Contracts.Metadata.EpisodeOrdering.AIRED)
			?? resource.Numbers.FirstOrDefault();
		if (chosen is null || chosen.SeasonNumber is null || chosen.Number is null)
		{
			return null;
		}

		return (chosen.SeasonNumber.Value, chosen.Number.Value, chosen.AbsoluteNumber);
	}

	private static Submarine.Core.Enums.SeriesStatus MapStatus(Contracts.Metadata.SeriesStatus status)
		=> status switch
		{
			Contracts.Metadata.SeriesStatus.CONTINUING => Submarine.Core.Enums.SeriesStatus.CONTINUING,
			Contracts.Metadata.SeriesStatus.ENDED => Submarine.Core.Enums.SeriesStatus.ENDED,
			Contracts.Metadata.SeriesStatus.UPCOMING => Submarine.Core.Enums.SeriesStatus.UPCOMING,
			_ => Submarine.Core.Enums.SeriesStatus.UNKNOWN
		};

	private static Submarine.Core.Enums.MovieStatus MapStatus(Contracts.Metadata.MovieStatus status)
		=> status switch
		{
			Contracts.Metadata.MovieStatus.ANNOUNCED => Submarine.Core.Enums.MovieStatus.ANNOUNCED,
			Contracts.Metadata.MovieStatus.IN_CINEMAS => Submarine.Core.Enums.MovieStatus.IN_CINEMAS,
			_ => Submarine.Core.Enums.MovieStatus.RELEASED
		};

	private static DateTime? ToUtc(DateOnly? date)
		=> date.HasValue ? DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : null;
}
