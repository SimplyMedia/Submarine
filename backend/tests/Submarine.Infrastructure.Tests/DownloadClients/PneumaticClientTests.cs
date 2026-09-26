using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public sealed class PneumaticClientTests : IDisposable
{
	private readonly string _root;
	private string NzbFolder => Path.Combine(_root, "nzb");
	private string StrmFolder => Path.Combine(_root, "strm");

	public PneumaticClientTests()
	{
		_root = Path.Combine(Path.GetTempPath(), $"submarine-pneumatic-{Guid.NewGuid():N}");
		Directory.CreateDirectory(NzbFolder);
		Directory.CreateDirectory(StrmFolder);
	}

	public void Dispose()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	private PneumaticClient CreateClient()
		=> new(new PneumaticSettings { NzbFolder = NzbFolder, StrmFolder = StrmFolder }, 5, "pneumatic",
			new HttpClient(new StubHttpHandler((_, _) => StubHttpHandler.Text("nzb-body"))));

	private static RemoteRelease NzbRelease(bool isSeasonPack = false)
		=> new()
		{
			Title = "Show.S01E01",
			DownloadUrl = "http://indexer/nzb/1",
			Size = 5,
			Protocol = Protocol.USENET,
			IsSeasonPack = isSeasonPack,
			Category = RemoteReleaseCategory.SERIES
		};

	[Fact]
	public void Type_ShouldBePneumatic()
	{
		var client = CreateClient();

		client.Type.ShouldBe(DownloadClientType.PNEUMATIC);
		client.Protocol.ShouldBe(Protocol.USENET);
	}

	[Fact]
	public async Task AddAsync_ShouldWriteNzbAndStrmFile_ReturningTitle()
	{
		var client = CreateClient();

		var id = await client.AddAsync(NzbRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("Show.S01E01");
		File.Exists(Path.Combine(NzbFolder, "Show.S01E01.nzb")).ShouldBeTrue();
		var strmPath = Path.Combine(StrmFolder, "Show.S01E01.strm");
		File.Exists(strmPath).ShouldBeTrue();
		(await File.ReadAllTextAsync(strmPath)).ShouldContain("plugin://plugin.program.pneumatic/");
	}

	[Fact]
	public async Task AddAsync_ShouldThrow_WhenReleaseIsSeasonPack()
	{
		var client = CreateClient();

		await Should.ThrowAsync<DownloadClientException>(() =>
			client.AddAsync(NzbRelease(isSeasonPack: true), null, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task GetItemsAsync_ShouldReturnCompletedItem_ForEachStrmFile()
	{
		var client = CreateClient();
		await client.AddAsync(NzbRelease(), null, TestContext.Current.CancellationToken);

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(1);
		items[0].DownloadId.ShouldBe("Show.S01E01");
		items[0].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[0].DownloadClientId.ShouldBe(5);
	}

	[Fact]
	public async Task RemoveAsync_ShouldThrow_BecauseRemovalIsNotSupported()
	{
		var client = CreateClient();

		await Should.ThrowAsync<DownloadClientException>(() =>
			client.RemoveAsync("Show.S01E01", false, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenFolderDoesNotExist()
	{
		var client = new PneumaticClient(
			new PneumaticSettings { NzbFolder = Path.Combine(_root, "missing"), StrmFolder = StrmFolder }, 5,
			"pneumatic", new HttpClient());

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("does not exist");
	}

	[Fact]
	public async Task TestAsync_ShouldSucceed_WhenBothFoldersAreWritable()
	{
		var client = CreateClient();

		await client.TestAsync(TestContext.Current.CancellationToken);
	}
}
