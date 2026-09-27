using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Features.Compat.Shared.Realtime;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Radarr;

/// <summary>
///     Forwards native movie and movie-file domain events onto the Radarr compatibility SignalR hub, in the
///     upstream Radarr "receiveMessage" shape, so Bazarr's realtime feed reacts to selected-version changes
///     without waiting for its next polling cycle. File-scoped events are filtered to the facade's currently
///     bound version so an unselected version's changes never masquerade as the facade movie's changes.
/// </summary>
public sealed class RadarrCompatEventForwarder(
	SubmarineDbContext db,
	CompatVersionSelection versions,
	IRadarrCompatRealtimeProjector projector,
	ICompatRealtimePublisher publisher)
	: IEventHandler<MovieAddedEvent>,
		IEventHandler<MovieUpdatedEvent>,
		IEventHandler<MovieDeletedEvent>,
		IEventHandler<MovieFileImportedEvent>,
		IEventHandler<MovieFileDeletedEvent>,
		IEventHandler<MediaRenamedEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(MovieAddedEvent @event, CancellationToken cancellationToken = default)
		=> PublishMovieAsync(@event.MovieId, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieUpdatedEvent @event, CancellationToken cancellationToken = default)
		=> PublishMovieAsync(@event.MovieId, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieDeletedEvent @event, CancellationToken cancellationToken = default)
		=> publisher.PublishRadarrAsync("movie", "deleted", new { id = @event.MovieId, tmdbId = @event.TmdbId, title = @event.Title, year = @event.Year }, cancellationToken);

	/// <inheritdoc />
	public async Task HandleAsync(MovieFileImportedEvent @event, CancellationToken cancellationToken = default)
	{
		if (await IsBoundVersionAsync(@event.MovieId, @event.MediaVersionId, cancellationToken))
		{
			await PublishMovieAsync(@event.MovieId, cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task HandleAsync(MovieFileDeletedEvent @event, CancellationToken cancellationToken = default)
	{
		if (await IsBoundVersionAsync(@event.MovieId, @event.MediaVersionId, cancellationToken))
		{
			await PublishMovieAsync(@event.MovieId, cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task HandleAsync(MediaRenamedEvent @event, CancellationToken cancellationToken = default)
	{
		if (@event.MovieId is not { } movieId)
		{
			return;
		}

		var binding = await versions.GetForMovieAsync(movieId, cancellationToken);
		if (binding?.MediaVersionId is not { } versionId)
		{
			return;
		}

		var version = await db.MediaVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
		var root = version is null ? null : await db.RootFolders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
		if (version is null || root is null)
		{
			return;
		}

		var versionPath = Path.GetFullPath(Path.Combine(root.Path, version.Path)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var touchesBoundVersion = @event.Files.Any(file => file.NewPath.StartsWith(versionPath, StringComparison.OrdinalIgnoreCase));
		if (touchesBoundVersion)
		{
			await PublishMovieAsync(movieId, cancellationToken);
		}
	}

	private async Task<bool> IsBoundVersionAsync(int movieId, int mediaVersionId, CancellationToken cancellationToken)
	{
		var binding = await versions.GetForMovieAsync(movieId, cancellationToken);
		return binding?.MediaVersionId == mediaVersionId;
	}

	private async Task PublishMovieAsync(int movieId, CancellationToken cancellationToken)
	{
		var resource = await projector.ProjectMovieAsync(movieId, cancellationToken);
		if (resource is not null)
		{
			await publisher.PublishRadarrAsync("movie", "updated", resource, cancellationToken);
		}
	}
}
