using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Submarine.Core.Enums;
using Submarine.Core.Net;
using Submarine.Infrastructure.Indexers;

namespace Submarine.Infrastructure.Http;

/// <summary>
///     Applies the outbound proxy and certificate validation snapshot to a
///     <see cref="SocketsHttpHandler" />, and answers whether a given URL bypasses the proxy.
///     Mirrors Sonarr's HttpProxySettingsProvider/X509CertificateValidationService: bypass
///     combines WebProxy's own local/host-list rules with an explicit CIDR bypass list, and
///     certificate errors are only ignored for local addresses or when validation is fully off.
/// </summary>
public static class OutboundProxyResolver
{
	/// <summary>
	///     Configures the handler's proxy (and, for HTTP, its bypass rules) for the given snapshot.
	///     SOCKS4/5 bypass is enforced per-connection in the connect callback since
	///     <see cref="SocketsHttpHandler.Proxy" /> only understands HTTP-style CONNECT proxies.
	/// </summary>
	public static void ApplyProxy(SocketsHttpHandler handler, OutboundProxySnapshot snapshot)
	{
		if (!snapshot.Enabled || string.IsNullOrWhiteSpace(snapshot.Host))
		{
			return;
		}

		switch (snapshot.Type)
		{
			case Submarine.Core.Enums.IndexerProxyType.HTTP:
				var webProxy = new WebProxy(snapshot.Host, snapshot.Port)
				{
					BypassProxyOnLocal = snapshot.BypassLocalAddresses,
					BypassList = ParseBypassList(snapshot.BypassFilter)
				};
				if (!string.IsNullOrEmpty(snapshot.Username))
				{
					webProxy.Credentials = new NetworkCredential(snapshot.Username, snapshot.Password);
				}

				handler.Proxy = webProxy;
				handler.UseProxy = true;
				break;

			case Submarine.Core.Enums.IndexerProxyType.SOCKS4:
			case Submarine.Core.Enums.IndexerProxyType.SOCKS5:
				handler.UseProxy = false;
				handler.ConnectCallback = async (context, cancellationToken) =>
				{
					if (ShouldBypass(snapshot, BuildUri(context.DnsEndPoint)))
					{
						var direct = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
						await direct.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
						return new NetworkStream(direct, ownsSocket: true);
					}

					var socket = snapshot.Type == Submarine.Core.Enums.IndexerProxyType.SOCKS5
						? await new Socks5Proxy(snapshot.Host, snapshot.Port, snapshot.Username, snapshot.Password)
							.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false)
						: await new Socks4Proxy(snapshot.Host, snapshot.Port, snapshot.Username)
							.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
					return new NetworkStream(socket, ownsSocket: true);
				};
				break;
		}
	}

	/// <summary>
	///     Whether a request to this URL should skip the proxy: WebProxy's own bypass-on-local
	///     and bypass-list rules, plus an explicit CIDR entry in the bypass list.
	/// </summary>
	public static bool ShouldBypass(OutboundProxySnapshot snapshot, Uri uri)
	{
		var probe = snapshot.GetBypassProbe();
		if (probe.IsBypassed(uri))
		{
			return true;
		}

		return IPAddress.TryParse(uri.Host, out var address)
			&& ParseBypassList(snapshot.BypassFilter)
				.Any(entry => IPNetwork.TryParse(entry, out var network) && network.Contains(address.Unmap()));
	}

	/// <summary>
	///     Splits the comma separated bypass filter into WebProxy's expected format, where a
	///     leading "*" domain wildcard becomes a leading ";" per <see cref="WebProxy.BypassList" />.
	/// </summary>
	public static string[] ParseBypassList(string bypassFilter)
	{
		if (string.IsNullOrWhiteSpace(bypassFilter))
		{
			return [];
		}

		var entries = bypassFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		for (var i = 0; i < entries.Length; i++)
		{
			if (entries[i].StartsWith('*'))
			{
				entries[i] = ";" + entries[i];
			}
		}

		return entries;
	}

	/// <summary>
	///     Whether a bypass filter can be compiled by <see cref="WebProxy" />.
	/// </summary>
	public static bool IsValidBypassFilter(string bypassFilter)
	{
		try
		{
			_ = new WebProxy(new Uri("http://localhost"), false, ParseBypassList(bypassFilter));
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}


	/// <summary>
	///     Certificate validation callback for outbound HttpClients, honouring
	///     <see cref="CertificateValidationType" />.
	/// </summary>
	public static bool ValidateCertificate(
		CertificateValidationType mode,
		object sender,
		X509Certificate? certificate,
		X509Chain? chain,
		SslPolicyErrors sslPolicyErrors)
	{
		if (sslPolicyErrors == SslPolicyErrors.None)
		{
			return true;
		}

		if (mode == CertificateValidationType.DISABLED)
		{
			return true;
		}

		if (mode != CertificateValidationType.DISABLED_FOR_LOCAL_ADDRESSES || sender is not SslStream { TargetHostName: { Length: > 0 } host })
		{
			return false;
		}

		if (host is "localhost")
		{
			return true;
		}

		try
		{
			var addresses = IPAddress.TryParse(host, out var direct) ? [direct] : Dns.GetHostAddresses(host);
			return addresses.Length > 0 && addresses.All(address => address.IsLocalAddress());
		}
		catch (SocketException)
		{
			return false;
		}
	}

	private static Uri BuildUri(DnsEndPoint endpoint) => new UriBuilder("https", endpoint.Host, endpoint.Port == 0 ? 443 : endpoint.Port).Uri;
}
