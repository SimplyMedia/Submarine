using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Blackhole;

/// <summary>
///     <see cref="IDownloadClient" /> which drops .nzb files into a watched folder for an external usenet
///     client to pick up, and reads its output back from a completed downloads folder
/// </summary>
public class UsenetBlackholeClient : IDownloadClient
{
	private readonly UsenetBlackholeSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<UsenetBlackholeClient> _logger;

	/// <summary>
	///     Creates a new <see cref="UsenetBlackholeClient" />
	/// </summary>
	/// <param name="settings">Folder settings</param>
	/// <param name="httpClient">http client used to download .nzb files</param>
	/// <param name="logger">The logger of this <see cref="UsenetBlackholeClient" /></param>
	public UsenetBlackholeClient(UsenetBlackholeSettings settings, HttpClient httpClient,
		ILogger<UsenetBlackholeClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
	}

	/// <inheritdoc />
	public Protocol Protocol
		=> Protocol.USENET;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		if (release.DownloadUrl == null)
			throw new DownloadClientException($"Release {release.Title} has no download url");

		var title = SanitizeFileName(release.Title);

		byte[] bytes;
		try
		{
			bytes = await _httpClient.GetByteArrayAsync(release.DownloadUrl, cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			throw new DownloadClientException($"Could not download the .nzb file for {release.Title}", ex);
		}

		await File.WriteAllBytesAsync(Path.Combine(_settings.NzbFolder, $"{title}.nzb"), bytes, cancellationToken);

		_logger.LogInformation("Dropped .nzb file for {Title} into {Folder}", release.Title, _settings.NzbFolder);
		return title;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult(ListWatchFolder(_settings.WatchFolder));

	/// <inheritdoc />
	public Task RemoveItemAsync(string downloadId, bool deleteData, CancellationToken cancellationToken = default)
	{
		RemoveWatchItem(_settings.WatchFolder, downloadId);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task TestAsync(CancellationToken cancellationToken = default)
	{
		VerifyWritable(_settings.NzbFolder);
		VerifyWritable(_settings.WatchFolder);
		return Task.CompletedTask;
	}

	private static IReadOnlyList<DownloadClientItem> ListWatchFolder(string watchFolder)
	{
		if (!Directory.Exists(watchFolder)) return [];

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
				OutputPath = directory
			});
		}

		foreach (var file in Directory.EnumerateFiles(watchFolder))
			items.Add(new DownloadClientItem
			{
				DownloadId = Path.GetFileName(file),
				Title = Path.GetFileName(file),
				TotalSize = new FileInfo(file).Length,
				Status = DownloadItemStatus.COMPLETED,
				OutputPath = file
			});

		return items;
	}

	private static void RemoveWatchItem(string watchFolder, string downloadId)
	{
		var path = Path.Combine(watchFolder, downloadId);

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

		throw new DownloadClientException($"No download {downloadId} found in {watchFolder}");
	}

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
			if (File.Exists(probePath)) File.Delete(probePath);
		}
	}

	private static string SanitizeFileName(string title)
	{
		var invalid = Path.GetInvalidFileNameChars();
		return string.Concat(title.Select(c => invalid.Contains(c) ? '_' : c));
	}
}
