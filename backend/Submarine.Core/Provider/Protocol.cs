namespace Submarine.Core.Provider;

/// <summary>
///     Supported Protocols of a <see cref="Provider" />
/// </summary>
public enum Protocol
{
	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/BitTorrent">Bittorrent</see> Protocol
	/// </summary>
	BITTORRENT,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Usenet">Usenet</see> Protocol
	/// </summary>
	USENET,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/XDCC">XDCC</see> Protocol, based on IRC
	/// </summary>
	XDCC
}
