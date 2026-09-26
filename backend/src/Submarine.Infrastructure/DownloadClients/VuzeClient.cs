using Submarine.Core.Download;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for Vuze, which exposes the same RPC api as Transmission
/// </summary>
public sealed class VuzeClient(
	TransmissionSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : TransmissionClient(settings, clientId, clientName, httpClient)
{
	private const int MinimumSupportedProtocolVersion = 14;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.VUZE;

	/// <inheritdoc />
	protected override bool SupportsLabels => false;

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		var result = await CallAsync("session-get", null, cancellationToken);
		var version = result["rpc-version"]?.GetValue<int>() ?? 0;

		if (version < MinimumSupportedProtocolVersion)
			throw new DownloadClientException(
				$"Vuze {ClientName} reports rpc protocol version {version}, but version {MinimumSupportedProtocolVersion} or higher is required");
	}

	/// <summary>
	///     Vuze reports the job's parent folder for multi-file torrents but the root download folder for
	///     single-file torrents; append the torrent name in the latter case so the output always points at
	///     the job's own folder or file
	/// </summary>
	protected override string? ResolveOutputPath(string? downloadDir, string name, int fileCount)
	{
		if (string.IsNullOrEmpty(downloadDir))
			return downloadDir;

		var trimmed = downloadDir.TrimEnd('/', '\\');
		var separatorIndex = trimmed.LastIndexOfAny(['/', '\\']);
		var lastSegment = separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..] : trimmed;

		if (lastSegment == name || fileCount > 1)
			return downloadDir;

		var separator = downloadDir.Contains('\\') && !downloadDir.Contains('/') ? '\\' : '/';
		return $"{trimmed}{separator}{name}";
	}
}
