using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for Pneumatic: downloads the nzb into a watched folder for Kodi's
///     Pneumatic addon and writes a companion .strm placeholder file that the addon resolves back into the
///     nzb; completed downloads are read back from the .strm files in the strm folder
/// </summary>
public sealed class PneumaticClient(
	PneumaticSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<PneumaticSettings>(settings, clientId, clientName, httpClient)
{
	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.PNEUMATIC;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.USENET;

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		if (release.IsSeasonPack)
			throw new DownloadClientException("Full season releases are not supported with Pneumatic");

		VerifyWritable(Settings.NzbFolder);
		VerifyWritable(Settings.StrmFolder);

		var title = SanitizeFileName(release.Title);
		var nzbData = await GetNzbDataAsync(release, cancellationToken);
		var nzbFile = Path.Combine(Settings.NzbFolder, $"{title}.nzb");
		await File.WriteAllBytesAsync(nzbFile, nzbData, cancellationToken);

		var strmFile = Path.Combine(Settings.StrmFolder, $"{title}.strm");
		var contents = $"plugin://plugin.program.pneumatic/?mode=strm&type=add_file&nzb={nzbFile}&nzbname={title}";
		await File.WriteAllTextAsync(strmFile, contents, cancellationToken);

		return title;
	}

	/// <inheritdoc />
	protected override Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		IReadOnlyList<DownloadClientItem> items = Directory.Exists(Settings.StrmFolder)
			?
			[
				.. Directory.EnumerateFiles(Settings.StrmFolder, "*.strm")
					.Select(file => new DownloadClientItem
					{
						DownloadId = Path.GetFileNameWithoutExtension(file),
						Title = Path.GetFileNameWithoutExtension(file),
						TotalSize = new FileInfo(file).Length,
						OutputPath = file,
						Status = DownloadItemStatus.COMPLETED
					})
			]
			: [];

		return Task.FromResult(items);
	}

	/// <inheritdoc />
	public override Task<DownloadClientStatus> GetStatusAsync(CancellationToken cancellationToken)
		=> Task.FromResult(new DownloadClientStatus([Settings.StrmFolder]));

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> throw new DownloadClientException("Pneumatic does not support removing downloads");

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
	{
		VerifyWritable(Settings.NzbFolder);
		VerifyWritable(Settings.StrmFolder);

		return Task.CompletedTask;
	}

	/// <summary>Verifies a folder exists and is writable</summary>
	/// <exception cref="DownloadClientException">The folder is missing or not writable</exception>
	private static void VerifyWritable(string folder)
	{
		if (!Directory.Exists(folder))
			throw new DownloadClientException($"Folder {folder} does not exist");

		var probePath = Path.Combine(folder, $".submarine-probe-{Guid.NewGuid():N}");
		try
		{
			File.WriteAllText(probePath, string.Empty);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			throw new DownloadClientException($"Folder {folder} is not writable", ex);
		}
		finally
		{
			if (File.Exists(probePath))
				File.Delete(probePath);
		}
	}
}
