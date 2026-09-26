using System.Net;

namespace Submarine.Core.Net;

/// <summary>
///     Resolves the real client address from a X-Forwarded-For chain, trusting only entries
///     appended by a proxy whose own address is in the configured trusted network list. Mirrors
///     the walk ASP.NET Core's ForwardedHeadersMiddleware performs with KnownNetworks: an entry is
///     only honoured while every hop closer to this server was itself a trusted proxy, so a
///     spoofed header from an untrusted peer is ignored.
/// </summary>
public static class ForwardedForResolver
{
	/// <summary>
	///     Resolves the effective client address.
	/// </summary>
	/// <param name="immediatePeer">The TCP connection's remote address.</param>
	/// <param name="forwardedFor">
	///     The X-Forwarded-For entries in header order (oldest/client first, nearest hop last).
	/// </param>
	/// <param name="trustedNetworks">Networks whose forwarded headers are trusted.</param>
	public static IPAddress Resolve(
		IPAddress immediatePeer,
		IReadOnlyList<string> forwardedFor,
		IReadOnlyList<IPNetwork> trustedNetworks)
	{
		var current = immediatePeer.Unmap();
		if (trustedNetworks.Count == 0)
		{
			return current;
		}

		var index = forwardedFor.Count - 1;
		while (IsTrusted(current, trustedNetworks) && index >= 0)
		{
			if (!IPAddress.TryParse(forwardedFor[index], out var candidate))
			{
				break;
			}

			current = candidate.Unmap();
			index--;
		}

		return current;
	}

	/// <summary>
	///     Parses a comma-separated CIDR list, silently skipping malformed entries.
	/// </summary>
	public static IReadOnlyList<IPNetwork> ParseNetworks(string? trustedProxies)
	{
		if (string.IsNullOrWhiteSpace(trustedProxies))
		{
			return [];
		}

		var networks = new List<IPNetwork>();
		foreach (var entry in trustedProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (IPNetwork.TryParse(entry, out var network))
			{
				networks.Add(network);
			}
		}

		return networks;
	}

	private static bool IsTrusted(IPAddress address, IReadOnlyList<IPNetwork> networks)
	{
		foreach (var network in networks)
		{
			if (network.Contains(address))
			{
				return true;
			}
		}

		return false;
	}
}
