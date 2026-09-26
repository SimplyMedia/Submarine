using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Refreshes a Trakt notification's access token shortly before it expires and persists the new token
///     pair back onto the notification row, since Trakt issues a new refresh token on every refresh.
/// </summary>
public interface ITraktTokenRefresher
{
	/// <summary>
	///     Returns the settings json to use for this delivery: unchanged for non-Trakt notifications or
	///     still-valid tokens, refreshed (and persisted) when the access token is near expiry.
	/// </summary>
	Task<string> EnsureFreshTokensAsync(Notification notification, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class TraktTokenRefresher(
	SubmarineDbContext db,
	ITraktAuthService authService,
	IConfiguration configuration,
	TimeProvider timeProvider) : ITraktTokenRefresher
{
	private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);

	/// <inheritdoc />
	public async Task<string> EnsureFreshTokensAsync(Notification notification, CancellationToken cancellationToken = default)
	{
		if (notification.Type != NotificationType.TRAKT)
		{
			return notification.SettingsJson;
		}

		var settings = (TraktSettings)NotificationSettingsJson.Parse(NotificationType.TRAKT, notification.SettingsJson);
		var now = timeProvider.GetUtcNow().UtcDateTime;
		if (settings.ExpiresAt > now + RefreshBuffer)
		{
			return notification.SettingsJson;
		}

		var clientId = TraktCredentials.RequireClientId(settings.ClientId, configuration);
		var clientSecret = TraktCredentials.RequireClientSecret(settings.ClientSecret, configuration);
		var refreshed = await authService.RefreshAsync(clientId, clientSecret, settings.RefreshToken, cancellationToken);

		var updated = settings with
		{
			AccessToken = refreshed.AccessToken,
			RefreshToken = refreshed.RefreshToken,
			ExpiresAt = now.AddSeconds(refreshed.ExpiresIn)
		};
		var json = NotificationSettingsJson.Serialize(updated);

		var row = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notification.Id, cancellationToken);
		if (row is not null)
		{
			row.SettingsJson = json;
			await db.SaveChangesAsync(cancellationToken);
		}

		return json;
	}
}
