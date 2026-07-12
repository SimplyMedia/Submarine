using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Models.Database;
using Submarine.Core.Notification;

namespace Submarine.Api.Events;

/// <summary>
///     Notifies enabled media server connections about grabbed, imported and renamed media
/// </summary>
public sealed class ConnectionEventHandler : IEventHandler<MediaGrabbedEvent>, IEventHandler<MediaImportedEvent>,
	IEventHandler<MediaRenamedEvent>
{
	private readonly SubmarineDatabaseContext _context;
	private readonly IMediaServerClientFactory _clientFactory;
	private readonly ILogger<ConnectionEventHandler> _logger;

	public ConnectionEventHandler(SubmarineDatabaseContext context, IMediaServerClientFactory clientFactory,
		ILogger<ConnectionEventHandler> logger)
	{
		_context = context;
		_clientFactory = clientFactory;
		_logger = logger;
	}

	public Task HandleAsync(MediaGrabbedEvent @event, CancellationToken cancellationToken)
		=> NotifyAsync(c => c.OnGrab, @event.SeriesId, @event.MovieId, @event.Path, cancellationToken);

	public Task HandleAsync(MediaImportedEvent @event, CancellationToken cancellationToken)
		=> NotifyAsync(c => c.OnImport, @event.SeriesId, @event.MovieId, @event.Path, cancellationToken);

	public Task HandleAsync(MediaRenamedEvent @event, CancellationToken cancellationToken)
		=> NotifyAsync(c => c.OnRename, @event.SeriesId, @event.MovieId, @event.Path, cancellationToken);

	private async Task NotifyAsync(Func<Connection, bool> toggle, int? seriesId, int? movieId, string path,
		CancellationToken cancellationToken)
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
				await _clientFactory.Create(connection).NotifyMediaUpdatedAsync(path, cancellationToken);
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
