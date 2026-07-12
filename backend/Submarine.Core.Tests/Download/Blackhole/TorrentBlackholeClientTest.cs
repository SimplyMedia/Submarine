using System.Net;
using Submarine.Core.Download;
using Submarine.Core.Download.Blackhole;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Core.Tests.Download.Blackhole;

public class TorrentBlackholeClientTest : IDisposable
{
	private readonly string _torrentFolder;
	private readonly string _watchFolder;
	private readonly ITestOutputHelper _output;

	public TorrentBlackholeClientTest(ITestOutputHelper output)
	{
		_output = output;
		_torrentFolder = CreateTempDirectory();
		_watchFolder = CreateTempDirectory();
	}

	public void Dispose()
	{
		Directory.Delete(_torrentFolder, true);
		Directory.Delete(_watchFolder, true);
	}

	[Fact]
	public async Task AddDownloadAsync_ShouldWriteTorrentFile_WhenDownloadUrlIsHttp()
	{
		var bytes = "fake-torrent-content"u8.ToArray();
		var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Movie 1", Guid = "guid-1", DownloadUrl = "https://indexer.example/1.torrent", Protocol = Protocol.BITTORRENT
		};

		var id = await client.AddDownloadAsync(release, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal("Movie 1", id);
		var filePath = Path.Combine(_torrentFolder, $"{id}.torrent");
		Assert.True(File.Exists(filePath));
		Assert.Equal(bytes, await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task AddDownloadAsync_ShouldWriteMagnetFile_WhenDownloadUrlIsAMagnetLink()
	{
		var client = CreateClient();
		var release = new ReleaseInfo
		{
			Title = "Movie 1", Guid = "guid-1", DownloadUrl = "magnet:?xt=urn:btih:ABCDEF1234567890", Protocol = Protocol.BITTORRENT
		};

		var id = await client.AddDownloadAsync(release, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal("Movie 1", id);
		var filePath = Path.Combine(_torrentFolder, $"{id}.magnet");
		Assert.True(File.Exists(filePath));
		Assert.Equal(release.DownloadUrl, await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task GetItemsAsync_ShouldListCompletedDirectoriesAndFiles_WhenWatchFolderHasContent()
	{
		var movieDirectory = Path.Combine(_watchFolder, "Movie.1");
		Directory.CreateDirectory(movieDirectory);
		await File.WriteAllBytesAsync(Path.Combine(movieDirectory, "movie.mkv"), new byte[1000], TestContext.Current.CancellationToken);
		await File.WriteAllBytesAsync(Path.Combine(_watchFolder, "Movie.2.torrent"), new byte[500], TestContext.Current.CancellationToken);

		var client = CreateClient();

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		Assert.Equal(2, items.Count);

		var directoryItem = items.Single(item => item.DownloadId == "Movie.1");
		Assert.Equal(DownloadItemStatus.COMPLETED, directoryItem.Status);
		Assert.Equal(1000, directoryItem.TotalSize);
		Assert.Equal(movieDirectory, directoryItem.OutputPath);

		var fileItem = items.Single(item => item.DownloadId == "Movie.2.torrent");
		Assert.Equal(DownloadItemStatus.COMPLETED, fileItem.Status);
		Assert.Equal(500, fileItem.TotalSize);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldDeleteMatchingWatchItem_WhenItExists()
	{
		var movieDirectory = Path.Combine(_watchFolder, "Movie.1");
		Directory.CreateDirectory(movieDirectory);
		await File.WriteAllTextAsync(Path.Combine(movieDirectory, "movie.mkv"), "data", TestContext.Current.CancellationToken);

		var client = CreateClient();

		await client.RemoveItemAsync("Movie.1", true, TestContext.Current.CancellationToken);

		Assert.False(Directory.Exists(movieDirectory));
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldThrowDownloadClientException_WhenItemDoesNotExist()
	{
		var client = CreateClient();

		await Assert.ThrowsAsync<DownloadClientException>(() => client.RemoveItemAsync("missing", true, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task TestAsync_ShouldNotThrow_WhenBothFoldersAreWritable()
	{
		var client = CreateClient();

		await client.TestAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task TestAsync_ShouldThrowDownloadClientException_WhenTorrentFolderDoesNotExist()
	{
		var settings = new TorrentBlackholeSettings
		{
			TorrentFolder = Path.Combine(_torrentFolder, "missing"), WatchFolder = _watchFolder
		};
		var client = new TorrentBlackholeClient(settings, new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
			new XunitLogger<TorrentBlackholeClient>(_output));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync(TestContext.Current.CancellationToken));
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(path);
		return path;
	}

	private TorrentBlackholeClient CreateClient(HttpMessageHandler? handler = null)
	{
		var settings = new TorrentBlackholeSettings { TorrentFolder = _torrentFolder, WatchFolder = _watchFolder };
		var httpClient = new HttpClient(handler ?? new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
		return new TorrentBlackholeClient(settings, httpClient, new XunitLogger<TorrentBlackholeClient>(_output));
	}

	private class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

		public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
			=> _responder = responder;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
			=> Task.FromResult(_responder(request));
	}
}
