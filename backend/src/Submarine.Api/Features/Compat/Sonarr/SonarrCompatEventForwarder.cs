using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Features.Compat.Shared.Realtime;
using Submarine.Core.Events;

namespace Submarine.Api.Features.Compat.Sonarr;

/// <summary>
///     Forwards native series, episode and episode-file domain events to the Sonarr compatibility hub. Filters
///     file events by the facade's selected version so an unselected sibling version never masquerades as the
///     facade's own file, and enriches the deletion event with a snapshot captured before the row was removed.
/// </summary>
public sealed class SonarrCompatEventForwarder(
	CompatVersionSelection versions,
	ISonarrCompatRealtimeProjector projector,
	ICompatRealtimePublisher publisher) :
	IEventHandler<SeriesAddedEvent>,
	IEventHandler<SeriesUpdatedEvent>,
	IEventHandler<SeriesDeletedEvent>,
	IEventHandler<EpisodeUpdatedEvent>,
	IEventHandler<EpisodeFileImportedEvent>,
	IEventHandler<EpisodeFileDeletedEvent>,
	IEventHandler<MediaRenamedEvent>
{
	public async Task HandleAsync(SeriesAddedEvent @event, CancellationToken cancellationToken = default)
		=> await PublishSeriesAsync(@event.SeriesId, cancellationToken);

	public async Task HandleAsync(SeriesUpdatedEvent @event, CancellationToken cancellationToken = default)
		=> await PublishSeriesAsync(@event.SeriesId, cancellationToken);

	public Task HandleAsync(SeriesDeletedEvent @event, CancellationToken cancellationToken = default)
		=> publisher.PublishSonarrAsync(
			"series", "deleted",
			new { id = @event.SeriesId, title = @event.Title, year = @event.Year },
			cancellationToken);

	public async Task HandleAsync(EpisodeUpdatedEvent @event, CancellationToken cancellationToken = default)
		=> await PublishEpisodeAsync(@event.EpisodeId, cancellationToken);

	public async Task HandleAsync(EpisodeFileImportedEvent @event, CancellationToken cancellationToken = default)
	{
		var binding = await versions.GetForSeriesAsync(@event.SeriesId, cancellationToken);
		if (binding?.MediaVersionId != @event.MediaVersionId)
		{
			return;
		}

		foreach (var episodeId in @event.EpisodeIds)
		{
			await PublishEpisodeAsync(episodeId, cancellationToken);
		}
	}

	public async Task HandleAsync(EpisodeFileDeletedEvent @event, CancellationToken cancellationToken = default)
	{
		var binding = await versions.GetForSeriesAsync(@event.SeriesId, cancellationToken);
		if (binding?.MediaVersionId != @event.MediaVersionId)
		{
			return;
		}

		foreach (var episodeId in @event.EpisodeIds)
		{
			await PublishEpisodeAsync(episodeId, cancellationToken);
		}
	}

	public async Task HandleAsync(MediaRenamedEvent @event, CancellationToken cancellationToken = default)
	{
		if (@event.SeriesId is { } seriesId)
		{
			await PublishSeriesAsync(seriesId, cancellationToken);
		}
	}

	private async Task PublishSeriesAsync(int seriesId, CancellationToken cancellationToken)
	{
		var resource = await projector.ProjectSeriesAsync(seriesId, cancellationToken);
		if (resource is not null)
		{
			await publisher.PublishSonarrAsync("series", "updated", resource, cancellationToken);
		}
	}

	private async Task PublishEpisodeAsync(int episodeId, CancellationToken cancellationToken)
	{
		var resource = await projector.ProjectEpisodeAsync(episodeId, cancellationToken);
		if (resource is not null)
		{
			await publisher.PublishSonarrAsync("episode", "updated", resource, cancellationToken);
		}
	}
}
