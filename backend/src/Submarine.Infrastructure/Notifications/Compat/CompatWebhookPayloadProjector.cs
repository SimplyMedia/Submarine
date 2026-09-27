using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Notifications.Compat;

public interface ICompatWebhookPayloadProjector
{
	Task<object> ProjectAsync(NotificationMessage message, string? facade, string? applicationUrl, CancellationToken cancellationToken = default);
}

public sealed class CompatWebhookPayloadProjector(SubmarineDbContext db) : ICompatWebhookPayloadProjector
{
	public async Task<object> ProjectAsync(NotificationMessage message, string? facade, string? applicationUrl, CancellationToken cancellationToken = default)
	{
		var app = message.MovieId is not null && message.SeriesId is null ? "radarr"
			: message.SeriesId is not null ? "sonarr"
			: (facade ?? "Sonarr").ToLowerInvariant();
		var instanceName = await db.GeneralConfig.AsNoTracking().Select(x => x.InstanceName).SingleAsync(cancellationToken);
		var eventType = message.EventType switch
		{
			NotificationEventType.GRAB => "Grab",
			NotificationEventType.IMPORT or NotificationEventType.UPGRADE => "Download",
			NotificationEventType.RENAME => "Rename",
			NotificationEventType.DELETE when message.SeriesId is not null => "SeriesDelete",
			NotificationEventType.DELETE when message.MovieId is not null => "MovieDelete",
			NotificationEventType.HEALTH => "Health",
			NotificationEventType.HEALTH_RESTORED => "HealthRestored",
			NotificationEventType.TEST => "Test",
			_ => message.EventType.ToString()
		};
		var envelope = new Dictionary<string, object?>
		{
			["eventType"] = eventType,
			["instanceName"] = instanceName,
			["applicationUrl"] = applicationUrl,
			["downloadClient"] = message.DownloadClient,
			["downloadId"] = message.DownloadId
		};
		if (app == "sonarr")
		{
			envelope["series"] = new { id = message.SeriesId, title = message.MediaTitle, year = message.Year, titleSlug = (string?)null, path = message.Path };
			envelope["episodes"] = message.Episodes.Select(x => new { id = (int?)null, seasonNumber = x.Season, episodeNumber = x.Number, title = x.Title, airDate = x.AirDate }).ToArray();
		}
		else
		{
			envelope["movie"] = new { id = message.MovieId, title = message.MediaTitle, year = message.Year, titleSlug = (string?)null, path = message.Path };
		}
		if (message.EventType is NotificationEventType.GRAB)
		{
			envelope["release"] = new
			{
				title = message.Title,
					indexer = message.Indexer,
					size = message.Size,
					quality = message.Quality is null ? null : new { quality = message.Quality.Resolution.Resolution.ToString(), source = message.Quality.Resolution.Source.ToString() },
					languages = message.Languages.Select(x => x.ToString()).ToArray(),
					releaseGroup = message.ReleaseGroup
			};
		}
		if (message.EventType is NotificationEventType.HEALTH or NotificationEventType.HEALTH_RESTORED)
		{
			envelope["source"] = message.Title;
			envelope["message"] = message.Body;
			envelope["type"] = "ApplicationUpdateCheck";
			envelope["wikiUrl"] = null;
			envelope["level"] = "warning";
		}
		return envelope;
	}
}
