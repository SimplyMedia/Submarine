using System.Net;
using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public abstract class BlackholeTestBase : IDisposable
{
	protected readonly string Root;
	protected string WatchFolder => Path.Combine(Root, "watch");
	protected string DropFolder => Path.Combine(Root, "drop");

	protected BlackholeTestBase()
	{
		Root = Path.Combine(Path.GetTempPath(), $"submarine-bh-{Guid.NewGuid():N}");
		Directory.CreateDirectory(WatchFolder);
		Directory.CreateDirectory(DropFolder);
	}

	public void Dispose()
	{
		if (Directory.Exists(Root))
			Directory.Delete(Root, true);
	}
}

public class TorrentBlackholeClientTests : BlackholeTestBase
{
	private TorrentBlackholeClient CreateClient(bool saveMagnetFiles = true, bool readOnly = false,
		string magnetExtension = ".magnet")
		=> new(new TorrentBlackholeSettings
		{
			TorrentFolder = DropFolder,
			WatchFolder = WatchFolder,
			SaveMagnetFiles = saveMagnetFiles,
			MagnetFileExtension = magnetExtension,
			ReadOnly = readOnly
		}, 12, "blackhole", new HttpClient());

	private static RemoteRelease TorrentRelease(byte[]? bytes = null)
		=> TestTorrent.Release(bytes);

	private static RemoteRelease MagnetRelease()
		=> new()
		{
			Title = "Some.Show.S01E01",
			DownloadUrl = "magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&dn=x",
			MagnetUrl = "magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&dn=x",
			Size = 5,
			Protocol = Protocol.BITTORRENT,
			Category = RemoteReleaseCategory.SERIES
		};

	[Fact]
	public async Task AddAsync_ShouldWriteTorrentFileAndReturnTitle()
	{
		var client = CreateClient();

		var id = await client.AddAsync(TorrentRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("Some.Show.S01E01");
		File.ReadAllBytes(Path.Combine(DropFolder, "Some.Show.S01E01.torrent")).ShouldBe(TestTorrent.Bytes);
	}

	[Fact]
	public async Task AddAsync_ShouldWriteMagnetFileWithConfiguredExtension()
	{
		var client = CreateClient(magnetExtension: ".magnet");

		var id = await client.AddAsync(MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("Some.Show.S01E01");
		File.ReadAllText(Path.Combine(DropFolder, "Some.Show.S01E01.magnet"))
			.ShouldContain("urn:btih:0123456789ABCDEF0123456789ABCDEF01234567");
	}

	[Fact]
	public async Task AddAsync_ShouldRejectMagnet_WhenSavingMagnetFilesDisabled()
	{
		var client = CreateClient(saveMagnetFiles: false);

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.AddAsync(MagnetRelease(), null, TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("magnet");
		Directory.GetFiles(DropFolder).ShouldBeEmpty();
	}

	[Fact]
	public async Task GetItemsAsync_ShouldListCompletedWatchFolderContent()
	{
		var completedDirectory = Path.Combine(WatchFolder, "Show.S01E01");
		Directory.CreateDirectory(completedDirectory);
		await File.WriteAllTextAsync(Path.Combine(completedDirectory, "file.mkv"), new string('x', 1024), TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(Path.Combine(WatchFolder, "Single.mkv"), new string('y', 512), TestContext.Current.CancellationToken);
		var client = CreateClient();

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(2);
		var directory = items.Single(item => item.DownloadId == "Show.S01E01");
		directory.Status.ShouldBe(DownloadItemStatus.COMPLETED);
		directory.OutputPath.ShouldBe(completedDirectory);
		directory.TotalSize.ShouldBe(1024);
		directory.IsReadOnly.ShouldBeFalse();
		directory.Protocol.ShouldBe(Protocol.BITTORRENT);
		directory.DownloadClientId.ShouldBe(12);
		directory.DownloadClientName.ShouldBe("blackhole");
		items.Single(item => item.DownloadId == "Single.mkv").TotalSize.ShouldBe(512);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMarkReadOnly_WhenReadOnlyConfigured()
	{
		await File.WriteAllTextAsync(Path.Combine(WatchFolder, "Done.mkv"), "data", TestContext.Current.CancellationToken);
		var client = CreateClient(readOnly: true);

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Single().IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public async Task GetStatusAsync_ShouldReturnWatchFolder()
	{
		var client = CreateClient();

		var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);

		status.OutputRootFolders.ShouldBe([WatchFolder]);
	}

	[Fact]
	public async Task RemoveAsync_ShouldDeleteFileFromWatchFolder()
	{
		var file = Path.Combine(WatchFolder, "Done.mkv");
		await File.WriteAllTextAsync(file, "data", TestContext.Current.CancellationToken);
		var client = CreateClient();

		await client.RemoveAsync("Done.mkv", true, TestContext.Current.CancellationToken);

		File.Exists(file).ShouldBeFalse();
	}

	[Fact]
	public async Task RemoveAsync_ShouldDeleteDirectoryFromWatchFolder()
	{
		var directory = Path.Combine(WatchFolder, "Show");
		Directory.CreateDirectory(directory);
		var client = CreateClient();

		await client.RemoveAsync("Show", false, TestContext.Current.CancellationToken);

		Directory.Exists(directory).ShouldBeFalse();
	}

	[Fact]
	public async Task RemoveAsync_ShouldThrow_WhenReadOnly()
	{
		var client = CreateClient(readOnly: true);

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.RemoveAsync("Anything", false, TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("read-only");
	}

	[Fact]
	public async Task RemoveAsync_ShouldThrow_WhenIdNotFound()
	{
		var client = CreateClient();

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.RemoveAsync("Ghost", false, TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("Ghost");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenDropFolderMissing()
	{
		var client = new TorrentBlackholeClient(new TorrentBlackholeSettings
		{
			TorrentFolder = Path.Combine(Root, "missing"),
			WatchFolder = WatchFolder
		}, 12, "blackhole", new HttpClient());

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("does not exist");
	}
}

public class UsenetBlackholeClientTests : BlackholeTestBase
{
	private UsenetBlackholeClient CreateClient()
		=> new(new UsenetBlackholeSettings { NzbFolder = DropFolder, WatchFolder = WatchFolder }, 13, "nzb-blackhole",
			new HttpClient());

	private static RemoteRelease Release(byte[]? nzbFile = null)
		=> new()
		{
			Title = "Some.Show.S01E01",
			DownloadUrl = "http://indexer/nzb/1",
			NzbFile = nzbFile,
			Size = 1000,
			Protocol = Protocol.USENET,
			Category = RemoteReleaseCategory.SERIES
		};

	[Fact]
	public async Task AddAsync_ShouldWriteNzbFile()
	{
		var client = CreateClient();

		var id = await client.AddAsync(Release([1, 2, 3]), null, TestContext.Current.CancellationToken);

		id.ShouldBe("Some.Show.S01E01");
		File.ReadAllBytes(Path.Combine(DropFolder, "Some.Show.S01E01.nzb")).ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task AddAsync_ShouldFetchNzbFromUrl_WhenFileMissing()
	{
		var handler = new StubHttpHandler((_, _) =>
			new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([4, 5, 6]) });
		var client = new UsenetBlackholeClient(
			new UsenetBlackholeSettings { NzbFolder = DropFolder, WatchFolder = WatchFolder }, 13, "nzb-blackhole",
			new HttpClient(handler));

		var id = await client.AddAsync(Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("Some.Show.S01E01");
		File.ReadAllBytes(Path.Combine(DropFolder, "Some.Show.S01E01.nzb")).ShouldBe([4, 5, 6]);
	}

	[Fact]
	public async Task GetItemsAndRemove_ShouldUseWatchFolder()
	{
		var file = Path.Combine(WatchFolder, "Done.nzb");
		await File.WriteAllTextAsync(file, "data", TestContext.Current.CancellationToken);
		var client = CreateClient();

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Single().DownloadId.ShouldBe("Done.nzb");
		items.Single().Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items.Single().Protocol.ShouldBe(Protocol.USENET);
		items.Single().DownloadClientId.ShouldBe(13);

		await client.RemoveAsync("Done.nzb", true, TestContext.Current.CancellationToken);
		File.Exists(file).ShouldBeFalse();
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenWatchFolderMissing()
	{
		var client = new UsenetBlackholeClient(
			new UsenetBlackholeSettings { NzbFolder = DropFolder, WatchFolder = Path.Combine(Root, "nope") }, 13,
			"nzb-blackhole", new HttpClient());

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("does not exist");
	}
}
