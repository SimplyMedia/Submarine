using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Notifications;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Turns domain events into notification messages and delivers them to enabled
///     notifications matching the event flags and tags. Sender failures are logged and swallowed.
/// </summary>
public sealed class NotificationDispatcher(
	SubmarineDbContext db,
	INotificationSenderFactory senderFactory,
	ILogger<NotificationDispatcher> logger) :
	IEventHandler<ReleaseGrabbedEvent>,
	IEventHandler<EpisodeFileImportedEvent>,
	IEventHandler<MovieFileImportedEvent>,
	IEventHandler<MediaRenamedEvent>,
	IEventHandler<EpisodeFileDeletedEvent>,
	IEventHandler<MovieFileDeletedEvent>,
	IEventHandler<SeriesDeletedEvent>,
	IEventHandler<MovieDeletedEvent>,
	IEventHandler<HealthIssuesChangedEvent>,
	IEventHandler<ManualInteractionRequiredEvent>,
	IEventHandler<ApplicationUpdateAvailableEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(ReleaseGrabbedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.GRAB, n => n.OnGrab, @event.Release.SeriesId, @event.Release.MovieId,
			async () =>
			{
				var release = @event.Release;
				var (title, year, imageUrl, episodes) = await LoadMediaAsync(release.SeriesId, release.MovieId, release.EpisodeIds, cancellationToken);
				return new NotificationMessage(
					NotificationEventType.GRAB,
					"Grabbed",
					FormatBody(title, episodes, release.Quality),
					release.SeriesId,
					release.MovieId,
					title,
					year,
					release.Quality,
					release.Languages,
					release.ReleaseGroup,
					release.Indexer,
					release.DownloadClientName,
					release.DownloadId,
					release.Size,
					null,
					imageUrl,
					episodes,
					BuildLinks(release.InfoUrl));
			}, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(EpisodeFileImportedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(@event.IsUpgrade ? NotificationEventType.UPGRADE : NotificationEventType.IMPORT,
			n => @event.IsUpgrade ? n.OnUpgrade : n.OnImport, @event.SeriesId, null,
			async () =>
			{
				var (title, year, imageUrl, episodes) = await LoadMediaAsync(@event.SeriesId, null, @event.EpisodeIds, cancellationToken);
				return ImportMessage(@event.IsUpgrade, title, year, imageUrl, episodes,
					@event.Quality, @event.Languages, @event.ReleaseGroup, @event.Path);
			}, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieFileImportedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(@event.IsUpgrade ? NotificationEventType.UPGRADE : NotificationEventType.IMPORT,
			n => @event.IsUpgrade ? n.OnUpgrade : n.OnImport, null, @event.MovieId,
			async () =>
			{
				var (title, year, imageUrl, _) = await LoadMediaAsync(null, @event.MovieId, [], cancellationToken);
				return ImportMessage(@event.IsUpgrade, title, year, imageUrl, [],
					@event.Quality, @event.Languages, @event.ReleaseGroup, @event.Path);
			}, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MediaRenamedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.RENAME, n => n.OnRename, @event.SeriesId, @event.MovieId,
			async () =>
			{
				var (title, year, imageUrl, _) = await LoadMediaAsync(@event.SeriesId, @event.MovieId, [], cancellationToken);
				var body = string.Join("\n", @event.Files.Select(f => $"{f.PreviousPath} -> {f.NewPath}"));
				return new NotificationMessage(
					NotificationEventType.RENAME,
					"Renamed",
					body,
					@event.SeriesId,
					@event.MovieId,
					title,
					year,
					null,
					[],
					null,
					null,
					null,
					null,
					null,
					@event.Files.FirstOrDefault()?.NewPath,
					imageUrl,
					[],
					[]);
			}, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(EpisodeFileDeletedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.DELETE, n => n.OnDelete, @event.SeriesId, null,
			async () =>
			{
				var (title, year, imageUrl, episodes) = await LoadMediaAsync(@event.SeriesId, null, @event.EpisodeIds, cancellationToken);
				return DeleteMessage(title, year, imageUrl, episodes, @event.Path);
			}, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieFileDeletedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.DELETE, n => n.OnDelete, null, @event.MovieId,
			async () =>
			{
				var (title, year, imageUrl, _) = await LoadMediaAsync(null, @event.MovieId, [], cancellationToken);
				return DeleteMessage(title, year, imageUrl, [], @event.Path);
			}, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(SeriesDeletedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.DELETE, n => n.OnDelete, @event.SeriesId, null,
			() => Task.FromResult(DeleteMessage(@event.Title, null, null, [], null)), cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieDeletedEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.DELETE, n => n.OnDelete, null, @event.MovieId,
			() => Task.FromResult(DeleteMessage(@event.Title, null, null, [], null)), cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(HealthIssuesChangedEvent @event, CancellationToken cancellationToken = default)
	{
		var issues = @event.Added
			.Select(issue => (
				Issue: issue,
				EventType: NotificationEventType.HEALTH,
				Flag: (Func<Notification, bool>)(n => n.OnHealthIssue
					&& (n.IncludeHealthWarnings || issue.Type == HealthIssueType.ERROR))))
			.Concat(@event.Restored.Select(issue => (
				Issue: issue,
				EventType: NotificationEventType.HEALTH_RESTORED,
				Flag: (Func<Notification, bool>)(n => n.OnHealthRestored))));

		return DispatchEachAsync(issues, cancellationToken);
	}

	private async Task DispatchEachAsync(
		IEnumerable<(HealthIssueSnapshot Issue, NotificationEventType EventType, Func<Notification, bool> Flag)> items,
		CancellationToken cancellationToken)
	{
		var notifications = await db.Notifications.AsNoTracking()
			.Where(n => n.Enable)
			.ToListAsync(cancellationToken);
		foreach (var (issue, eventType, flag) in items)
		{
			var message = new NotificationMessage(
				eventType,
				eventType == NotificationEventType.HEALTH ? "Health issue" : "Health restored",
				$"{issue.Source}: {issue.Message}",
				null,
				null,
				string.Empty,
				null,
				null,
				[],
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				[],
				issue.WikiUrl is null ? [] : [new NotificationLink("Wiki", issue.WikiUrl)]);
			await SendAsync(notifications.Where(flag), message, cancellationToken);
		}
	}

	/// <inheritdoc />
	public Task HandleAsync(ManualInteractionRequiredEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.MANUAL_INTERACTION, n => n.OnManualInteractionRequired,
			@event.SeriesId, @event.MovieId,
			async () =>
			{
				var (title, year, imageUrl, episodes) = await LoadMediaAsync(@event.SeriesId, @event.MovieId, [], cancellationToken);
				return new NotificationMessage(
					NotificationEventType.MANUAL_INTERACTION,
					"Manual interaction required",
					$"{@event.Title}: {@event.Message}",
					@event.SeriesId,
					@event.MovieId,
					title,
					year,
					null,
					[],
					null,
					null,
					null,
					@event.DownloadId,
					null,
					null,
					imageUrl,
					episodes,
					[]);
			}, cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(ApplicationUpdateAvailableEvent @event, CancellationToken cancellationToken = default)
		=> DispatchAsync(NotificationEventType.APPLICATION_UPDATE, n => n.OnApplicationUpdate, null, null,
			() => Task.FromResult(new NotificationMessage(
				NotificationEventType.APPLICATION_UPDATE,
				"Update available",
				$"Version {@event.NewVersion} is available, installed is {@event.CurrentVersion}.",
				null,
				null,
				string.Empty,
				null,
				null,
				[],
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				[],
				[new NotificationLink("Release notes", @event.ReleaseNotesUrl)])), cancellationToken);

	private async Task DispatchAsync(
		NotificationEventType eventType,
		Func<Notification, bool> flag,
		int? seriesId,
		int? movieId,
		Func<Task<NotificationMessage>> buildMessage,
		CancellationToken cancellationToken)
	{
		var notifications = await db.Notifications.AsNoTracking()
			.Include(n => n.Tags)
			.Where(n => n.Enable)
			.ToListAsync(cancellationToken);
		var matching = notifications.Where(flag).ToList();

		if (seriesId is not null || movieId is not null)
		{
			var mediaTags = await LoadMediaTagsAsync(seriesId, movieId, cancellationToken);
			matching = matching
				.Where(n => n.Tags.Count == 0 || n.Tags.Any(tag => mediaTags.Contains(tag.Label)))
				.ToList();
		}

		if (matching.Count == 0)
		{
			return;
		}

		var message = await buildMessage();
		await SendAsync(matching, message, cancellationToken);
	}

	private async Task SendAsync(IEnumerable<Notification> notifications, NotificationMessage message, CancellationToken cancellationToken)
	{
		foreach (var notification in notifications)
		{
			if (notification.Type is NotificationType.PLEX or NotificationType.EMBY or NotificationType.JELLYFIN
				&& message.EventType
					is not (NotificationEventType.IMPORT
						or NotificationEventType.UPGRADE
						or NotificationEventType.RENAME
						or NotificationEventType.DELETE))
			{
				continue;
			}

			INotificationSender sender;
			try
			{
				sender = senderFactory.Resolve(notification.Type);
			}
			catch (InvalidOperationException ex)
			{
				logger.LogWarning("Notification {Name} skipped: {Message}", notification.Name, ex.Message);
				continue;
			}

			try
			{
				await sender.SendAsync(message, notification.SettingsJson, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Notification {Name} ({Type}) failed for {Event}",
					notification.Name, notification.Type, message.EventType);
			}
		}
	}

	private async Task<(string Title, int? Year, string? ImageUrl, List<NotificationEpisode> Episodes)> LoadMediaAsync(
		int? seriesId,
		int? movieId,
		IReadOnlyList<int> episodeIds,
		CancellationToken cancellationToken)
	{
		if (seriesId is { } sid)
		{
			var series = await db.Series.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
			var episodes = await db.Episodes.AsNoTracking()
				.Where(x => x.SeriesId == sid && episodeIds.Contains(x.Id))
				.OrderBy(x => x.SeasonNumber).ThenBy(x => x.EpisodeNumber)
				.Select(x => new NotificationEpisode(x.SeasonNumber, x.EpisodeNumber, x.Title, x.AirDate))
				.ToListAsync(cancellationToken);
			return (series?.Title ?? "Deleted series", series?.Year, series?.PosterUrl, episodes);
		}

		if (movieId is { } mid)
		{
			var movie = await db.Movies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mid, cancellationToken);
			return (movie?.Title ?? "Deleted movie", movie?.Year, movie?.PosterUrl, []);
		}

		return (string.Empty, null, null, []);
	}

	private async Task<HashSet<string>> LoadMediaTagsAsync(int? seriesId, int? movieId, CancellationToken cancellationToken)
	{
		IEnumerable<string> labels;
		if (seriesId is { } sid)
		{
			labels = await db.Series.AsNoTracking()
				.Where(x => x.Id == sid)
				.SelectMany(x => x.Tags)
				.Select(t => t.Label)
				.ToListAsync(cancellationToken);
		}
		else if (movieId is { } mid)
		{
			labels = await db.Movies.AsNoTracking()
				.Where(x => x.Id == mid)
				.SelectMany(x => x.Tags)
				.Select(t => t.Label)
				.ToListAsync(cancellationToken);
		}
		else
		{
			labels = [];
		}

		return labels.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static NotificationMessage ImportMessage(
		bool isUpgrade,
		string title,
		int? year,
		string? imageUrl,
		List<NotificationEpisode> episodes,
		QualityModel quality,
		IReadOnlyList<Language> languages,
		string? releaseGroup,
		string path)
		=> new(
			isUpgrade ? NotificationEventType.UPGRADE : NotificationEventType.IMPORT,
			isUpgrade ? "Upgraded" : "Imported",
			FormatBody(title, episodes, quality),
			null,
			null,
			title,
			year,
			quality,
			languages,
			releaseGroup,
			null,
			null,
			null,
			null,
			path,
			imageUrl,
			episodes,
			[]);

	private static NotificationMessage DeleteMessage(
		string title,
		int? year,
		string? imageUrl,
		List<NotificationEpisode> episodes,
		string? path)
		=> new(
			NotificationEventType.DELETE,
			"Deleted",
			path is null ? title : $"{title}\n{path}",
			null,
			null,
			title,
			year,
			null,
			[],
			null,
			null,
			null,
			null,
			null,
			path,
			imageUrl,
			episodes,
			[]);

	private static string FormatBody(string title, List<NotificationEpisode> episodes, QualityModel? quality)
	{
		var body = new StringBuilder(title);
		var episodeText = FormatEpisodes(episodes);
		if (episodeText.Length > 0)
		{
			body.Append(" - ").Append(episodeText);
		}

		var qualityText = FormatQuality(quality);
		if (qualityText.Length > 0)
		{
			body.Append(" [").Append(qualityText).Append(']');
		}

		return body.ToString();
	}

	/// <summary>Formats episodes as SxxEyy identifiers joined by commas.</summary>
	public static string FormatEpisodes(IReadOnlyList<NotificationEpisode> episodes)
		=> string.Join(", ", episodes.Select(e => $"S{e.Season:00}E{e.Number:00}"));

	/// <summary>Formats a quality model for display, empty when absent.</summary>
	public static string FormatQuality(QualityModel? quality)
	{
		if (quality?.Resolution is null)
		{
			return string.Empty;
		}

		var text = quality.Resolution.Name;
		if (quality.Revision.IsProper)
		{
			text += " Proper";
		}
		else if (quality.Revision.IsRepack)
		{
			text += " Repack";
		}
		else if (quality.Revision.Version > 1)
		{
			text += $" v{quality.Revision.Version}";
		}

		return text;
	}

	private static IReadOnlyList<NotificationLink> BuildLinks(string? infoUrl)
		=> string.IsNullOrEmpty(infoUrl) ? [] : [new NotificationLink("Info", infoUrl)];
}
