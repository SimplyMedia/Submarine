namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which sends a push notification via a self-hosted Gotify server
/// </summary>
public class GotifyConnection : Connection
{
	/// <summary>
	///     Url of the Gotify server to send messages to
	/// </summary>
	public string ServerUrl { get; set; }

	/// <summary>
	///     Application token used to authenticate against the Gotify server
	/// </summary>
	public string AppToken { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="GotifyConnection" />
	/// </summary>
	public GotifyConnection()
		=> Type = ConnectionType.GOTIFY;
}
