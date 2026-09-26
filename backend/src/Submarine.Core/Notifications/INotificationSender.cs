using Submarine.Core.Enums;

namespace Submarine.Core.Notifications;

/// <summary>
///     Sends notification messages for one notification type.
///     Implementations are stateless; settings arrive per call.
/// </summary>
public interface INotificationSender
{
	/// <summary>Notification type this sender handles.</summary>
	NotificationType Type { get; }

	/// <summary>
	///     Send a message.
	/// </summary>
	/// <param name="message">Message to deliver.</param>
	/// <param name="settingsJson">Type specific settings as JSON.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SendAsync(NotificationMessage message, string settingsJson, CancellationToken cancellationToken = default);

	/// <summary>
	///     Verify the settings work by sending a test message.
	/// </summary>
	/// <param name="settingsJson">Type specific settings as JSON.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task TestAsync(string settingsJson, CancellationToken cancellationToken = default);
}
