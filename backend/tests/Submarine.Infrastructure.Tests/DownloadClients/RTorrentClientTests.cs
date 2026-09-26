using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class RTorrentClientTests
{
	private static RTorrentSettings Settings()
		=> new() { Host = "rt.local", Port = 8080, UrlBase = "RPC2", Category = "tv" };

	private static string MulticallResponse(params (string Hash, string Name, long Size, long Left, long Complete, long Active, long State, string Directory, string Label)[] rows)
	{
		var rowsXml = string.Join(string.Empty, rows.Select(row => $"""
				<value><array><data>
					<value><string>{row.Hash}</string></value>
					<value><string>{row.Name}</string></value>
					<value><i8>{row.Size}</i8></value>
					<value><i8>{row.Left}</i8></value>
					<value><i8>{row.Complete}</i8></value>
					<value><i8>{row.Active}</i8></value>
					<value><i8>{row.State}</i8></value>
					<value><string>{row.Directory}</string></value>
					<value><string>{row.Label}</string></value>
				</data></array></value>
			"""));

		return $"""
			<?xml version="1.0"?>
			<methodResponse><params><param><value><array><data>{rowsXml}</data></array></value></param></params></methodResponse>
			""";
	}

	[Fact]
	public async Task AddAsync_ShouldLoadStartMagnetWithLabel_WhenMagnetRelease()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return StubHttpHandler.Xml("""
				<?xml version="1.0"?><methodResponse><params><param><value><i8>0</i8></value></param></params></methodResponse>
				""");
		});
		var client = new RTorrentClient(Settings(), 2, "rt", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("0123456789ABCDEF0123456789ABCDEF01234567");
		requestBody!.ShouldContain("<methodName>load.start</methodName>");
		requestBody!.ShouldContain("magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&amp;dn=x");
		requestBody!.ShouldContain("d.custom1.set=tv");
		handler.Requests[0].Url.ShouldBe("http://rt.local:8080/RPC2");
	}

	[Fact]
	public async Task AddAsync_ShouldLoadRawStartTorrentBase64_WhenTorrentRelease()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return StubHttpHandler.Xml("""
				<?xml version="1.0"?><methodResponse><params><param><value><i8>0</i8></value></param></params></methodResponse>
				""");
		});
		var client = new RTorrentClient(Settings(), 2, "rt", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe(TestTorrent.InfoHash);
		requestBody!.ShouldContain("<methodName>load.raw_start</methodName>");
		requestBody!.ShouldContain(Convert.ToBase64String(TestTorrent.Bytes));
	}

	[Fact]
	public async Task AddAsync_ShouldUseStoppedVariants_WhenAddStopped()
	{
		var methods = new List<string>();
		var handler = new StubHttpHandler((_, body) =>
		{
			methods.Add(body!);
			return StubHttpHandler.Xml("""
				<?xml version="1.0"?><methodResponse><params><param><value><i8>0</i8></value></param></params></methodResponse>
				""");
		});
		var client = new RTorrentClient(Settings() with { AddStopped = true, Directory = "/data/tv" }, 2, "rt",
			new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		methods[0].ShouldContain("<methodName>load.stop</methodName>");
		methods[0].ShouldContain("d.directory_base.set=/data/tv");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapMulticallRowsAndIdentity()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Xml(MulticallResponse(
			("AAAA", "Show.S01E01", 1000, 400, 0, 1, 1, "/data/tv", "tv"),
			("BBBB", "Seeding", 2000, 0, 1, 1, 1, "/data/tv", "tv"),
			("CCCC", "Stopped", 3000, 3000, 0, 0, 0, "/data/tv", "tv"),
			("DDDD", "Foreign", 4000, 0, 1, 1, 1, "/data", "other"))));
		var client = new RTorrentClient(Settings(), 2, "rt", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(4);
		items[0].DownloadId.ShouldBe("AAAA");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].RemainingSize.ShouldBe(400);
		items[0].OutputPath.ShouldBe("/data/tv");
		items[0].Category.ShouldBe("tv");
		items[0].IsReadOnly.ShouldBeFalse();
		items[0].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[0].DownloadClientId.ShouldBe(2);
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[2].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[3].IsReadOnly.ShouldBeTrue();

		handler.Requests[0].Body!.ShouldContain("<methodName>d.multicall2</methodName>");
		handler.Requests[0].Body!.ShouldContain("d.custom1=");
	}

	[Fact]
	public async Task RemoveAsync_ShouldEraseTorrent()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return StubHttpHandler.Xml("""
				<?xml version="1.0"?><methodResponse><params><param><value><i8>0</i8></value></param></params></methodResponse>
				""");
		});
		var client = new RTorrentClient(Settings(), 2, "rt", new HttpClient(handler));

		await client.RemoveAsync("AAAA", true, TestContext.Current.CancellationToken);

		requestBody!.ShouldContain("<methodName>d.erase</methodName>");
		requestBody!.ShouldContain("AAAA");
	}

	[Fact]
	public async Task TestAsync_ShouldThrowWithFaultString_WhenXmlRpcFault()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Xml("""
			<?xml version="1.0"?>
			<methodResponse><fault><value><struct>
				<member><name>faultCode</name><value><i4>-501</i4></value></member>
				<member><name>faultString</name><value><string>Auth failure</string></value></member>
			</struct></value></fault></methodResponse>
			"""));
		var client = new RTorrentClient(Settings(), 2, "rt", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("Auth failure");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldSetCustom1_WhenPostImportCategoryDiffersFromCategory()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return StubHttpHandler.Xml("""
				<?xml version="1.0"?><methodResponse><params><param><value><i8>0</i8></value></param></params></methodResponse>
				""");
		});
		var client = new RTorrentClient(Settings() with { PostImportCategory = "imported" }, 2, "rt",
			new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		requestBody!.ShouldContain("<methodName>d.custom1.set</methodName>");
		requestBody!.ShouldContain("AAAA");
		requestBody!.ShouldContain("imported");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldNotCall_WhenPostImportCategoryMatchesCategory()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Xml("""
			<?xml version="1.0"?><methodResponse><params><param><value><i8>0</i8></value></param></params></methodResponse>
			"""));
		var client = new RTorrentClient(Settings() with { PostImportCategory = "tv" }, 2, "rt", new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		handler.Requests.Count.ShouldBe(0);
	}
}
