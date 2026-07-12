using Submarine.Core.Notification;

namespace Submarine.Api.Clients;

/// <summary>
///     Message sent to a notification <see cref="Connection" /> for a media event
/// </summary>
/// <param name="EventType">"grab", "import", "upgrade", "rename", "delete", "health" or "test"</param>
/// <param name="Title">Title of the series or movie the event relates to</param>
/// <param name="Path">Library path of the media, if any</param>
/// <param name="Quality">Quality of the media, if known</param>
/// <param name="Timestamp">Time the event occurred</param>
/// <param name="SeriesId">Id of the series the event relates to, if any</param>
/// <param name="MovieId">Id of the movie the event relates to, if any</param>
public sealed record NotificationMessage(string EventType, string Title, string? Path, string? Quality,
	DateTimeOffset Timestamp, int? SeriesId = null, int? MovieId = null)
{
	/// <summary>
	///     Human readable message text for the event
	/// </summary>
	public string DisplayText => EventType switch
	{
		"grab" => $"Grabbed: {Title}",
		"import" => $"Imported: {Title}",
		"upgrade" => $"Upgraded: {Title}",
		"rename" => $"Renamed: {Title}",
		"delete" => $"Deleted: {Title}",
		"health" => $"Health issue: {Title}",
		_ => Title
	};
}

/// <summary>
///     Sends notification messages for a notification <see cref="Connection" /> (Discord, Telegram, generic webhook)
/// </summary>
public interface INotificationSender
{
	/// <summary>
	///     Sends a notification message for a media event
	/// </summary>
	/// <param name="message">message to send</param>
	/// <param name="cancellationToken">cancellation token</param>
	Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);

	/// <summary>
	///     Sends a test notification to verify connectivity and authentication
	/// </summary>
	/// <param name="cancellationToken">cancellation token</param>
	Task TestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Creates <see cref="INotificationSender" /> instances for notification <see cref="Connection" /> rows
/// </summary>
public interface INotificationSenderFactory
{
	/// <summary>
	///     Creates a sender for the given connection
	/// </summary>
	/// <param name="connection">connection to create a sender for</param>
	/// <returns>notification sender</returns>
	INotificationSender Create(Connection connection);
}
