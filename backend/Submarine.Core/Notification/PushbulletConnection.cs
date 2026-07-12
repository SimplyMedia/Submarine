namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which sends a push notification via Pushbullet
/// </summary>
public class PushbulletConnection : Connection
{
	/// <summary>
	///     Pushbullet access token used to send messages
	/// </summary>
	public string AccessToken { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="PushbulletConnection" />
	/// </summary>
	public PushbulletConnection()
		=> Type = ConnectionType.PUSHBULLET;
}
