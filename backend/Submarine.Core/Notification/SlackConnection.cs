namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which notifies a Slack channel via an incoming webhook
/// </summary>
public class SlackConnection : Connection
{
	/// <summary>
	///     Slack incoming webhook url to post messages to
	/// </summary>
	public string WebhookUrl { get; set; } = string.Empty;

	/// <summary>
	///     Creates a new instance of <see cref="SlackConnection" />
	/// </summary>
	public SlackConnection()
		=> Type = ConnectionType.SLACK;
}
