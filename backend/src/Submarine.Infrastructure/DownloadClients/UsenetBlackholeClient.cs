using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> which drops .nzb files into a watched folder for an external usenet
///     client to pick up, and reads completed downloads back from a watch folder
/// </summary>
public sealed class UsenetBlackholeClient(
	UsenetBlackholeSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : BlackholeClientBase<UsenetBlackholeSettings>(settings, clientId, clientName, httpClient)
{
	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.USENET_BLACKHOLE;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.USENET;

	/// <inheritdoc />
	protected override string WatchFolder => Settings.WatchFolder;

	/// <inheritdoc />
	protected override bool ReadOnly => false;

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		VerifyWritable(Settings.NzbFolder);

		var nzbData = await GetNzbDataAsync(release, cancellationToken);
		var title = SanitizeFileName(release.Title);

		await File.WriteAllBytesAsync(Path.Combine(Settings.NzbFolder, $"{title}.nzb"), nzbData, cancellationToken);

		return title;
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
	{
		RemoveWatchItem(downloadId);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
	{
		VerifyWritable(Settings.NzbFolder);
		VerifyWritable(Settings.WatchFolder);

		return Task.CompletedTask;
	}
}
