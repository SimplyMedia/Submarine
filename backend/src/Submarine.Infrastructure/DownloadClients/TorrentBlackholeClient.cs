using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> which drops .torrent/.magnet files into a watched folder for an
///     external torrent client to pick up, and reads completed downloads back from a watch folder
/// </summary>
public sealed class TorrentBlackholeClient(
	TorrentBlackholeSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : BlackholeClientBase<TorrentBlackholeSettings>(settings, clientId, clientName, httpClient)
{
	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.TORRENT_BLACKHOLE;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	/// <inheritdoc />
	protected override string WatchFolder => Settings.WatchFolder;

	/// <inheritdoc />
	protected override bool ReadOnly => Settings.ReadOnly;

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		VerifyWritable(Settings.TorrentFolder);

		var title = SanitizeFileName(release.Title);
		var magnetUrl = MagnetUrl(release);

		if (magnetUrl is not null)
		{
			if (!Settings.SaveMagnetFiles)
				throw new DownloadClientException(
					$"Release {release.Title} is a magnet link; enable saving magnet files or provide the .torrent file");

			await File.WriteAllTextAsync(Path.Combine(Settings.TorrentFolder, title + Settings.MagnetFileExtension),
				magnetUrl, cancellationToken);

			return title;
		}

		var torrentData = await GetTorrentDataAsync(release, cancellationToken);
		await File.WriteAllBytesAsync(Path.Combine(Settings.TorrentFolder, $"{title}.torrent"), torrentData,
			cancellationToken);

		return title;
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
	{
		if (Settings.ReadOnly)
			throw new DownloadClientException(
				$"Torrent blackhole {ClientName} is read-only; downloads are managed elsewhere");

		RemoveWatchItem(downloadId);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
	{
		VerifyWritable(Settings.TorrentFolder);
		VerifyWritable(Settings.WatchFolder);

		return Task.CompletedTask;
	}
}
