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
		var resource = await _metadataClient.GetSeriesAsync(series.TvdbId, cancellationToken);

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

		foreach (var episodeResource in resource.Episodes)
		{
			var aired = episodeResource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Aired);
			var absolute = episodeResource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Absolute);

			var seasonNumber = aired?.SeasonNumber ?? 0;
			var episodeNumber = aired?.Number ?? 0;

			var existing = episodes.FirstOrDefault(e =>
				e.SeasonNumber == seasonNumber && e.EpisodeNumber == episodeNumber);

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
				await _eventPublisher.PublishAsync(
					new EpisodeTitleChangedEvent(existing.Id, series.Id, oldTitle, newTitle!), cancellationToken);

			existing.Title = newTitle;
			existing.Overview = episodeResource.Overview;
			existing.AirDate = airDate;
			existing.Runtime = episodeResource.Runtime;
			existing.AbsoluteEpisodeNumber = absolute?.AbsoluteNumber;
		}

		await _context.SaveChangesAsync(cancellationToken);
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
