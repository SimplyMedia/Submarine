namespace Submarine.Core.Notification;

/// <summary>
///     A Connection which notifies a Kodi media player to update its library
/// </summary>
public class KodiConnection : Connection
{
	/// <summary>
	///     Username for optional authentication against the Kodi JSON-RPC API
	/// </summary>
	public string? Username { get; set; }

	/// <summary>
	///     Password for optional authentication against the Kodi JSON-RPC API
	/// </summary>
	public string? Password { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="KodiConnection" />
	/// </summary>
	public KodiConnection()
		=> Type = ConnectionType.KODI;
}
