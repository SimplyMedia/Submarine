namespace Submarine.Core.Download;

/// <summary>
///     Creates configured download clients
/// </summary>
public interface IDownloadClientFactory
{
	/// <summary>
	///     Creates a download client for the given type and settings
	/// </summary>
	/// <param name="type">Client type to create</param>
	/// <param name="settingsJson">Type-specific settings as JSON</param>
	/// <param name="clientId">Submarine id of the client row</param>
	/// <param name="clientName">Display name of the client row</param>
	/// <exception cref="DownloadClientException">The settings are invalid for the type</exception>
	IDownloadClient Create(DownloadClientType type, string settingsJson, int clientId, string clientName);
}
