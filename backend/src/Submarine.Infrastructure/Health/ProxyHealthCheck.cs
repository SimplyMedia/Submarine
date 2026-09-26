using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Checks that the configured outbound proxy host resolves and accepts a TCP connection.
/// </summary>
public sealed class ProxyHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Proxy";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var config = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		if (!config.ProxyEnabled)
		{
			return [];
		}

		IPAddress[] addresses;
		try
		{
			addresses = await Dns.GetHostAddressesAsync(config.ProxyHost, cancellationToken);
		}
		catch (Exception ex) when (ex is SocketException or ArgumentException)
		{
			return [new(HealthIssueType.ERROR, Source, $"Could not resolve the outbound proxy host '{config.ProxyHost}'", null)];
		}

		if (addresses.Length == 0)
		{
			return [new(HealthIssueType.ERROR, Source, $"Could not resolve the outbound proxy host '{config.ProxyHost}'", null)];
		}

		try
		{
			using var client = new TcpClient();
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(5));
			await client.ConnectAsync(addresses[0], config.ProxyPort, timeout.Token);
		}
		catch (Exception ex) when (ex is SocketException or OperationCanceledException)
		{
			return [new(HealthIssueType.ERROR, Source, $"Could not reach the outbound proxy at {config.ProxyHost}:{config.ProxyPort}", null)];
		}

		return [];
	}
}
