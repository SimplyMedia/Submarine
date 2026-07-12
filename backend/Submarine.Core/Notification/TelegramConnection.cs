namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which notifies a Telegram chat via a bot
/// </summary>
public class TelegramConnection : Connection
{
	/// <summary>
	///     Token of the Telegram bot used to send messages
	/// </summary>
	public string BotToken { get; set; } = string.Empty;

	/// <summary>
	///     Id of the Telegram chat to send messages to
	/// </summary>
	public string ChatId { get; set; } = string.Empty;

	/// <summary>
	///     Creates a new instance of <see cref="TelegramConnection" />
	/// </summary>
	public TelegramConnection()
		=> Type = ConnectionType.TELEGRAM;
}
