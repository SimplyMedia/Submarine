using System.Net;
using Submarine.Core.Download;
using Submarine.Core.Download.Blackhole;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Core.Tests.Download.Blackhole;

public class UsenetBlackholeClientTest : IDisposable
{
	private readonly string _nzbFolder;
	private readonly string _watchFolder;
	private readonly ITestOutputHelper _output;

	public UsenetBlackholeClientTest(ITestOutputHelper output)
	{
		_output = output;
		_nzbFolder = CreateTempDirectory();
		_watchFolder = CreateTempDirectory();
	}

	public void Dispose()
	{
		Directory.Delete(_nzbFolder, true);
		Directory.Delete(_watchFolder, true);
	}

	[Fact]
	public async Task AddDownloadAsync_ShouldWriteNzbFile_WhenDownloadUrlIsHttp()
	{
		var bytes = "fake-nzb-content"u8.ToArray();
		var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Show S01E01", Guid = "guid-1", DownloadUrl = "https://indexer.example/1.nzb", Protocol = Protocol.USENET
		};

		var id = await client.AddDownloadAsync(release);

		Assert.Equal("Show S01E01", id);
		var filePath = Path.Combine(_nzbFolder, $"{id}.nzb");
		Assert.True(File.Exists(filePath));
		Assert.Equal(bytes, await File.ReadAllBytesAsync(filePath));
	}

	[Fact]
	public async Task GetItemsAsync_ShouldListCompletedDirectoriesAndFiles_WhenWatchFolderHasContent()
	{
		var showDirectory = Path.Combine(_watchFolder, "Show.S01E01");
		Directory.CreateDirectory(showDirectory);
		await File.WriteAllBytesAsync(Path.Combine(showDirectory, "episode.mkv"), new byte[2000]);
		await File.WriteAllBytesAsync(Path.Combine(_watchFolder, "Show.S01E02.nzb"), new byte[300]);

		var client = CreateClient();

		var items = await client.GetItemsAsync();

		Assert.Equal(2, items.Count);

		var directoryItem = items.Single(item => item.DownloadId == "Show.S01E01");
		Assert.Equal(DownloadItemStatus.COMPLETED, directoryItem.Status);
		Assert.Equal(2000, directoryItem.TotalSize);
		Assert.Equal(showDirectory, directoryItem.OutputPath);

		var fileItem = items.Single(item => item.DownloadId == "Show.S01E02.nzb");
		Assert.Equal(DownloadItemStatus.COMPLETED, fileItem.Status);
		Assert.Equal(300, fileItem.TotalSize);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldDeleteMatchingWatchItem_WhenItExists()
	{
		var showDirectory = Path.Combine(_watchFolder, "Show.S01E01");
		Directory.CreateDirectory(showDirectory);
		await File.WriteAllTextAsync(Path.Combine(showDirectory, "episode.mkv"), "data");

		var client = CreateClient();

		await client.RemoveItemAsync("Show.S01E01", true);

		Assert.False(Directory.Exists(showDirectory));
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldThrowDownloadClientException_WhenItemDoesNotExist()
	{
		var client = CreateClient();

		await Assert.ThrowsAsync<DownloadClientException>(() => client.RemoveItemAsync("missing", true));
	}

	[Fact]
	public async Task TestAsync_ShouldNotThrow_WhenBothFoldersAreWritable()
	{
		var client = CreateClient();

		await client.TestAsync();
	}

	[Fact]
	public async Task TestAsync_ShouldThrowDownloadClientException_WhenWatchFolderDoesNotExist()
	{
		var settings = new UsenetBlackholeSettings { NzbFolder = _nzbFolder, WatchFolder = Path.Combine(_watchFolder, "missing") };
		var client = new UsenetBlackholeClient(settings, new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
			new XunitLogger<UsenetBlackholeClient>(_output));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(path);
		return path;
	}

	private UsenetBlackholeClient CreateClient(HttpMessageHandler? handler = null)
	{
		var settings = new UsenetBlackholeSettings { NzbFolder = _nzbFolder, WatchFolder = _watchFolder };
		var httpClient = new HttpClient(handler ?? new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
		return new UsenetBlackholeClient(settings, httpClient, new XunitLogger<UsenetBlackholeClient>(_output));
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
