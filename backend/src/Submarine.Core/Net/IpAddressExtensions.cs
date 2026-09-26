using System.Net;
using System.Net.Sockets;

namespace Submarine.Core.Net;

/// <summary>
///     Local-address classification shared by the authentication local bypass and outbound
///     certificate validation local bypass.
/// </summary>
public static class IpAddressExtensions
{
	/// <summary>
	///     True for loopback, RFC 1918 private ranges, link-local (169.254.0.0/16) and IPv6
	///     link-local, unique-local or site-local addresses.
	/// </summary>
	public static bool IsLocalAddress(this IPAddress address)
	{
		address = address.Unmap();

		if (IPAddress.IsLoopback(address))
		{
			return true;
		}

		if (address.AddressFamily == AddressFamily.InterNetwork)
		{
			var bytes = address.GetAddressBytes();
			var isLinkLocal = bytes[0] == 169 && bytes[1] == 254;
			var isClassA = bytes[0] == 10;
			var isClassB = bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
			var isClassC = bytes[0] == 192 && bytes[1] == 168;
			return isLinkLocal || isClassA || isClassB || isClassC;
		}

		if (address.AddressFamily == AddressFamily.InterNetworkV6)
		{
			return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal || address.IsIPv6SiteLocal;
		}

		return false;
	}

	/// <summary>
	///     Maps an IPv4-mapped IPv6 address (e.g. "::ffff:1.2.3.4") back to IPv4, otherwise
	///     returns the address unchanged.
	/// </summary>
	public static IPAddress Unmap(this IPAddress address)
		=> address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
