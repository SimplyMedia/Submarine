using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Models.Database;
using Submarine.Core.Notification;

namespace Submarine.Api.Events;

/// <summary>
///     Notifies enabled connections about grabbed, imported, upgraded, renamed and deleted media and health issues
/// </summary>
public sealed class ConnectionEventHandler : IEventHandler<MediaGrabbedEvent>, IEventHandler<MediaImportedEvent>,
	IEventHandler<MediaRenamedEvent>, IEventHandler<MediaDeletedEvent>, IEventHandler<HealthIssueEvent>
{
	private readonly SubmarineDatabaseContext _context;
	private readonly IMediaServerClientFactory _clientFactory;
	private readonly INotificationSenderFactory _notificationSenderFactory;
	private readonly ILogger<ConnectionEventHandler> _logger;

	public ConnectionEventHandler(SubmarineDatabaseContext context, IMediaServerClientFactory clientFactory,
		INotificationSenderFactory notificationSenderFactory, ILogger<ConnectionEventHandler> logger)
	{
		_context = context;
		_clientFactory = clientFactory;
		_notificationSenderFactory = notificationSenderFactory;
		_logger = logger;
	}

	public Task HandleAsync(MediaGrabbedEvent @event, CancellationToken cancellationToken)
		=> NotifyAsync(c => c.OnGrab, "grab", @event.SeriesId, @event.MovieId, @event.Path, @event.Title,
			cancellationToken);

	public async Task HandleAsync(MediaImportedEvent @event, CancellationToken cancellationToken)
	{
		await NotifyAsync(c => c.OnImport, "import", @event.SeriesId, @event.MovieId, @event.Path, @event.Title,
			cancellationToken);

		// OnImport fires on all imports including upgrades; OnUpgrade is a distinct toggle notified with its
		// own event type. Connections with both toggles are only notified once, via OnImport.
		if (@event.IsUpgrade)
			await NotifyAsync(c => c.OnUpgrade && !c.OnImport, "upgrade", @event.SeriesId, @event.MovieId,
				@event.Path, @event.Title, cancellationToken);
	}

	public Task HandleAsync(MediaRenamedEvent @event, CancellationToken cancellationToken)
		=> NotifyAsync(c => c.OnRename, "rename", @event.SeriesId, @event.MovieId, @event.Path, @event.Title,
			cancellationToken);

	public Task HandleAsync(MediaDeletedEvent @event, CancellationToken cancellationToken)
		=> NotifyAsync(c => c.OnDelete, "delete", @event.SeriesId, @event.MovieId, @event.Path, @event.Title,
			cancellationToken);

	public Task HandleAsync(HealthIssueEvent @event, CancellationToken cancellationToken)
		=> NotifyAsync(c => c.OnHealthIssue, "health", null, null, null, $"{@event.Source}: {@event.Message}",
			cancellationToken);

	private async Task NotifyAsync(Func<Connection, bool> toggle, string eventType, int? seriesId, int? movieId,
		string? path, string title, CancellationToken cancellationToken)
	{
		var connections = await _context.Connections.AsNoTracking()
			.Where(c => c.Enable)
			.ToListAsync(cancellationToken);

		connections = connections.Where(toggle).ToList();

		if (connections.Count == 0)
			return;

		var mediaTags = await LoadMediaTagsAsync(seriesId, movieId, cancellationToken);

		foreach (var connection in connections)
		{
			if (connection.Tags.Count > 0 && !connection.Tags.Intersect(mediaTags).Any())
				continue;

			try
			{
				if (connection.Type is ConnectionType.PLEX or ConnectionType.EMBY or ConnectionType.JELLYFIN)
				{
					// media servers only refresh their library; deletes and health issues carry no path change
					if (path != null && eventType is not ("delete" or "health"))
						await _clientFactory.Create(connection).NotifyMediaUpdatedAsync(path, cancellationToken);
				}
				else
				{
					var message = new NotificationMessage(eventType, title, path, null, DateTimeOffset.UtcNow,
						seriesId, movieId);
					await _notificationSenderFactory.Create(connection).SendAsync(message, cancellationToken);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Notifying connection {Name} failed", connection.Name);
			}
		}
	}

	private async Task<List<string>> LoadMediaTagsAsync(int? seriesId, int? movieId,
		CancellationToken cancellationToken)
	{
		if (seriesId != null)
			return await _context.Series.AsNoTracking()
				.Where(s => s.Id == seriesId)
				.Select(s => s.Tags)
				.FirstOrDefaultAsync(cancellationToken) ?? new List<string>();

		if (movieId != null)
			return await _context.Movies.AsNoTracking()
				.Where(m => m.Id == movieId)
				.Select(m => m.Tags)
				.FirstOrDefaultAsync(cancellationToken) ?? new List<string>();

		return new List<string>();
	}
}
