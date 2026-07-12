using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Events;
using Submarine.Api.Models.Database;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using MetadataSeriesStatus = Submarine.Metadata.Contracts.SeriesStatus;

namespace Submarine.Api.Services;

public class SeriesRefreshService
{
	private readonly SubmarineDatabaseContext _context;
	private readonly IMetadataClient _metadataClient;
	private readonly IEventPublisher _eventPublisher;
	private readonly ILogger<SeriesRefreshService> _logger;

	public SeriesRefreshService(SubmarineDatabaseContext context, IMetadataClient metadataClient,
		IEventPublisher eventPublisher, ILogger<SeriesRefreshService> logger)
	{
		_context = context;
		_metadataClient = metadataClient;
		_eventPublisher = eventPublisher;
		_logger = logger;
	}

	public async Task RefreshSeriesAsync(Series series, CancellationToken cancellationToken = default)
	{
		var resource = await FetchAsync(series, cancellationToken);

		if (resource == null)
		{
			_logger.LogWarning("Metadata for series {SeriesId} (TVDB {TvdbId}) not found", series.Id, series.TvdbId);
			return;
		}

		series.Title = resource.Title;
		series.Overview = resource.Overview;
		series.Status = MapStatus(resource.Status);
		series.Network = resource.Network;
		series.Runtime = resource.Runtime;

		var seasons = await _context.Seasons.Where(s => s.SeriesId == series.Id).ToListAsync(cancellationToken);

		foreach (var seasonResource in resource.Seasons)
			if (seasons.All(s => s.SeasonNumber != seasonResource.SeasonNumber))
				_context.Seasons.Add(new Season
				{
					SeriesId = series.Id, SeasonNumber = seasonResource.SeasonNumber, Monitored = series.Monitored
				});

		var episodes = await _context.Episodes.Where(e => e.SeriesId == series.Id)
			.Include(e => e.Files)
			.ToListAsync(cancellationToken);

		var titleChanges = new List<EpisodeTitleChangedEvent>();

		foreach (var episodeResource in resource.Episodes)
		{
			var absolute = episodeResource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Absolute);
			var (seasonNumber, episodeNumber) = EpisodeNumberResolver.Resolve(episodeResource, series.Numbering);

			var existing = FindExisting(episodes, episodeResource, seasonNumber, episodeNumber);

			var airDate = episodeResource.AirDate == null
				? (DateTimeOffset?)null
				: new DateTimeOffset(episodeResource.AirDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

			if (existing == null)
			{
				_context.Episodes.Add(new Episode
				{
					SeriesId = series.Id,
					SeasonNumber = seasonNumber,
					EpisodeNumber = episodeNumber,
					AbsoluteEpisodeNumber = absolute?.AbsoluteNumber,
					TvdbId = episodeResource.TvdbId,
					TmdbId = episodeResource.TmdbId,
					Title = episodeResource.Title,
					Overview = episodeResource.Overview,
					AirDate = airDate,
					Runtime = episodeResource.Runtime,
					Monitored = series.Monitored
				});

				continue;
			}

			var oldTitle = existing.Title;
			var newTitle = episodeResource.Title;

			if (ShouldPublishTitleChange(existing, oldTitle, newTitle))
				titleChanges.Add(new EpisodeTitleChangedEvent(existing.Id, series.Id, oldTitle, newTitle!));

			existing.SeasonNumber = seasonNumber;
			existing.EpisodeNumber = episodeNumber;
			existing.TvdbId = episodeResource.TvdbId;
			existing.TmdbId = episodeResource.TmdbId;
			existing.Title = newTitle;
			existing.Overview = episodeResource.Overview;
			existing.AirDate = airDate;
			existing.Runtime = episodeResource.Runtime;
			existing.AbsoluteEpisodeNumber = absolute?.AbsoluteNumber;
		}

		await _context.SaveChangesAsync(cancellationToken);

		// Publish only after the new titles are persisted so the queued handler cannot read a stale title
		foreach (var change in titleChanges)
			await _eventPublisher.PublishAsync(change, cancellationToken);
	}

	private async Task<SeriesResource?> FetchAsync(Series series, CancellationToken cancellationToken)
	{
		if (series.MetadataProvider == MetadataProvider.TMDB)
		{
			if (series.TmdbId != null)
				return await _metadataClient.GetSeriesByTmdbAsync(series.TmdbId.Value, cancellationToken);

			_logger.LogWarning(
				"Series {SeriesId} uses TMDB metadata but has no TMDB id, falling back to TVDB", series.Id);
		}

		return await _metadataClient.GetSeriesByTvdbAsync(series.TvdbId, cancellationToken);
	}

	private static Episode? FindExisting(List<Episode> episodes, EpisodeResource resource, int seasonNumber,
		int episodeNumber)
	{
		if (resource.TvdbId != null)
		{
			var match = episodes.FirstOrDefault(e => e.TvdbId == resource.TvdbId);

			if (match != null)
				return match;
		}

		if (resource.TmdbId != null)
		{
			var match = episodes.FirstOrDefault(e => e.TmdbId == resource.TmdbId);

			if (match != null)
				return match;
		}

		return episodes.FirstOrDefault(e => e.SeasonNumber == seasonNumber && e.EpisodeNumber == episodeNumber);
	}

	public async Task RefreshMovieAsync(Movie movie, CancellationToken cancellationToken = default)
	{
		var resource = await _metadataClient.GetMovieAsync(movie.TmdbId, cancellationToken);

		if (resource == null)
		{
			_logger.LogWarning("Metadata for movie {MovieId} (TMDB {TmdbId}) not found", movie.Id, movie.TmdbId);
			return;
		}

		movie.Title = resource.Title;
		movie.Overview = resource.Overview;
		movie.Runtime = resource.Runtime;
		movie.Studio = resource.Studio;
		movie.Year = resource.Year;
		movie.TmdbCollectionId = resource.TmdbCollectionId;
		movie.CollectionTitle = resource.CollectionTitle;
		movie.ReleaseDate = resource.ReleaseDate == null
			? null
			: new DateTimeOffset(resource.ReleaseDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

		await _context.SaveChangesAsync(cancellationToken);
	}

	private static bool ShouldPublishTitleChange(Episode episode, string? oldTitle, string? newTitle)
	{
		if (episode.Files.Count == 0 || string.IsNullOrEmpty(newTitle))
			return false;

		if (string.Equals(oldTitle, newTitle, StringComparison.Ordinal))
			return false;

		var namedFromPlaceholder = episode.Files.Any(f => f.NamedFromPlaceholder);

		return !string.IsNullOrEmpty(oldTitle) || namedFromPlaceholder;
	}

	private static Core.Library.SeriesStatus MapStatus(MetadataSeriesStatus status)
		=> status switch
		{
			MetadataSeriesStatus.Continuing => Core.Library.SeriesStatus.CONTINUING,
			MetadataSeriesStatus.Ended => Core.Library.SeriesStatus.ENDED,
			MetadataSeriesStatus.Upcoming => Core.Library.SeriesStatus.UPCOMING,
			_ => Core.Library.SeriesStatus.UNKNOWN
		};
}
