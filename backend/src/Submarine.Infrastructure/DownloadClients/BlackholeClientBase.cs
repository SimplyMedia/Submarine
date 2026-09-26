using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     Shared watch folder behaviour for blackhole clients: completed downloads appear as folders or
///     files in a folder watched by Submarine
/// </summary>
public abstract class BlackholeClientBase<TSettings>(
	TSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<TSettings>(settings, clientId, clientName, httpClient)
	where TSettings : DownloadClientSettings
{
	/// <summary>Folder the external client places completed downloads into</summary>
	protected abstract string WatchFolder { get; }

	/// <summary>Whether the watch folder is managed elsewhere</summary>
	protected abstract bool ReadOnly { get; }

	/// <inheritdoc />
	protected override Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
		=> Task.FromResult(ListWatchFolder(WatchFolder, ReadOnly));

	/// <inheritdoc />
	public override Task<DownloadClientStatus> GetStatusAsync(CancellationToken cancellationToken)
		=> Task.FromResult(new DownloadClientStatus([WatchFolder]));

	/// <summary>Deletes the completed download (folder or file) with the given id from the watch folder</summary>
	/// <exception cref="DownloadClientException">The id is not found in the watch folder</exception>
	protected void RemoveWatchItem(string downloadId)
	{
		var path = Path.Combine(WatchFolder, downloadId);

		if (Directory.Exists(path))
		{
			Directory.Delete(path, true);
			return;
		}

		if (File.Exists(path))
		{
			File.Delete(path);
			return;
		}

		throw new DownloadClientException($"No download {downloadId} found in {WatchFolder}");
	}

	/// <summary>Verifies a folder exists and is writable</summary>
	/// <exception cref="DownloadClientException">The folder is missing or not writable</exception>
	protected static void VerifyWritable(string folder)
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

	private static IReadOnlyList<DownloadClientItem> ListWatchFolder(string watchFolder, bool readOnly)
	{
		if (!Directory.Exists(watchFolder))
			return [];

		var items = new List<DownloadClientItem>();

		foreach (var directory in Directory.EnumerateDirectories(watchFolder))
		{
			var size = new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories)
				.Sum(file => file.Length);

			items.Add(new DownloadClientItem
			{
				DownloadId = Path.GetFileName(directory),
				Title = Path.GetFileName(directory),
				TotalSize = size,
				Status = DownloadItemStatus.COMPLETED,
				OutputPath = directory,
				IsReadOnly = readOnly
			});
		}

		items.AddRange(Directory.EnumerateFiles(watchFolder).Select(file => new DownloadClientItem
		{
			DownloadId = Path.GetFileName(file),
			Title = Path.GetFileName(file),
			TotalSize = new FileInfo(file).Length,
			Status = DownloadItemStatus.COMPLETED,
			OutputPath = file,
			IsReadOnly = readOnly
		}));

		return items;
	}
}
