namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which notifies a Discord channel via an incoming webhook
/// </summary>
public class DiscordConnection : Connection
{
	/// <summary>
	///     Discord incoming webhook url to post messages to
	/// </summary>
	public string WebhookUrl { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="DiscordConnection" />
	/// </summary>
	public DiscordConnection()
		=> Type = ConnectionType.DISCORD;
}
