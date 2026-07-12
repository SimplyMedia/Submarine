using Submarine.Core.Notification;

namespace Submarine.Api.Clients;

/// <summary>
///     Client for a media server a <see cref="Connection" /> points at
/// </summary>
public interface IMediaServerClient
{
	/// <summary>
	///     Notifies the media server that media at the given library path was added or changed
	/// </summary>
	/// <param name="path">library path of the media</param>
	/// <param name="cancellationToken">cancellation token</param>
	Task NotifyMediaUpdatedAsync(string path, CancellationToken cancellationToken = default);

	/// <summary>
	///     Tests connectivity and authentication against the media server
	/// </summary>
	/// <param name="cancellationToken">cancellation token</param>
	Task TestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Creates <see cref="IMediaServerClient" /> instances for <see cref="Connection" /> rows
/// </summary>
public interface IMediaServerClientFactory
{
	/// <summary>
	///     Creates a client for the given connection
	/// </summary>
	/// <param name="connection">connection to create a client for</param>
	/// <returns>media server client</returns>
	IMediaServerClient Create(Connection connection);
}
