using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     An enabled download client row paired with its constructed client instance.
/// </summary>
/// <param name="Entity">The configured download client row.</param>
/// <param name="Instance">The constructed client used to talk to it.</param>
public sealed record EnabledDownloadClient(DownloadClient Entity, IDownloadClient Instance);

/// <summary>
///     Constructs download client instances for the enabled, configured download clients.
/// </summary>
public interface IDownloadClientProvider
{
	/// <summary>
	///     All enabled download clients, ordered by priority, optionally filtered by protocol.
	/// </summary>
	Task<IReadOnlyList<EnabledDownloadClient>> GetEnabledAsync(Protocol? protocol = null, CancellationToken cancellationToken = default);

	/// <summary>
	///     A single enabled download client by id, or null when it does not exist or is disabled.
	/// </summary>
	Task<EnabledDownloadClient?> GetAsync(int id, CancellationToken cancellationToken = default);
}
