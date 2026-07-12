namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which sends a push notification via Pushover
/// </summary>
public class PushoverConnection : Connection
{
	/// <summary>
	///     Pushover application token used to send messages
	/// </summary>
	public string AppToken { get; set; }

	/// <summary>
	///     Pushover user or group key to send messages to
	/// </summary>
	public string UserKey { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="PushoverConnection" />
	/// </summary>
	public PushoverConnection()
		=> Type = ConnectionType.PUSHOVER;
}
