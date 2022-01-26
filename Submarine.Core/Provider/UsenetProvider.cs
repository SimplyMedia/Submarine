namespace Submarine.Core.Provider;

/// <summary>
///     A Usenet Provider is a Provider which serves files via <see cref="Protocol.USENET" /> and
///     <see href="https://en.wikipedia.org/wiki/NZB">NZB</see> files
/// </summary>
public class UsenetProvider : Provider
{
	/// <summary>
	///     Creates a new instance of <see cref="UsenetProvider" />
	/// </summary>
	public UsenetProvider()
		=> Protocol = Protocol.USENET;
}
