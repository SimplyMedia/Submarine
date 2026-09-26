using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Resolves the sender for a notification type.
/// </summary>
public interface INotificationSenderFactory
{
	/// <summary>
	///     Gets the sender handling the given type.
	/// </summary>
	/// <exception cref="InvalidOperationException">No sender is registered for the type.</exception>
	INotificationSender Resolve(NotificationType type);
}

/// <summary>
///     Default factory picking from all registered senders.
/// </summary>
public sealed class NotificationSenderFactory(IEnumerable<INotificationSender> senders) : INotificationSenderFactory
{
	/// <summary>Name of the shared notification http client.</summary>
	public const string HttpClientName = "notifications";

	/// <summary>Timeout applied to notification http calls.</summary>
	public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

	/// <inheritdoc />
	public INotificationSender Resolve(NotificationType type)
		=> senders.FirstOrDefault(sender => sender.Type == type)
			?? throw new InvalidOperationException($"No notification sender registered for {type}");
}
