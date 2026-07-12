using System.Net;
using System.Net.Http;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class UTorrentClientTest
{
	private const string Magnet = "magnet:?xt=urn:btih:abcdef1234567890abcdef1234567890abcdef12&dn=Ubuntu";

	private const string ListResponse = """
		{"build":12345,"torrents":[
			["HASHXYZ",201,"Ubuntu.iso",2000,250,500,0,0,0,100,120,"movies",5,0,0,0,0,0,1500,"",0,0,0,0,0,0,"/downloads"]
		]}
		""";

	private readonly ITestOutputHelper _output;

	public UTorrentClientTest(ITestOutputHelper output)
		=> _output = output;

	private static string Token(string value)
		=> $"<html><div id='token' style='display:none;'>{value}</div></html>";

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnMagnetHashAndUseToken_WhenReleaseIsMagnet()
	{
		var (client, handler) = CreateClient((HttpStatusCode.OK, Token("TOKENABC")), (HttpStatusCode.OK, "{}"));

		var id = await client.AddDownloadAsync(new ReleaseInfo { Title = "Ubuntu", Guid = "g", DownloadUrl = Magnet }, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal("ABCDEF1234567890ABCDEF1234567890ABCDEF12", id);
		Assert.EndsWith("/gui/token.html", handler.Requests[0].AbsolutePath);
		Assert.Contains("token=TOKENABC", handler.Requests[1].Query);
		Assert.Contains("action=add-url", handler.Requests[1].Query);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTorrentArray_WhenUTorrentReturnsList()
	{
		var (client, _) = CreateClient((HttpStatusCode.OK, Token("T")), (HttpStatusCode.OK, ListResponse));

		var item = Assert.Single(await client.GetItemsAsync(TestContext.Current.CancellationToken));

		Assert.Equal("HASHXYZ", item.DownloadId);
		Assert.Equal("Ubuntu.iso", item.Title);
		Assert.Equal(2000, item.TotalSize);
		Assert.Equal(1500, item.RemainingSize);
		Assert.Equal(TimeSpan.FromSeconds(120), item.RemainingTime);
		Assert.Equal(DownloadItemStatus.DOWNLOADING, item.Status);
		Assert.Equal("movies", item.Category);
		Assert.Equal("/downloads", item.OutputPath);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldRefreshTokenAndRetryOnce_WhenServerRejectsToken()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, Token("TOKONE")),
			(HttpStatusCode.BadRequest, "invalid request"),
			(HttpStatusCode.OK, Token("TOKTWO")),
			(HttpStatusCode.OK, ListResponse));

		await client.GetItemsAsync(TestContext.Current.CancellationToken);

		Assert.Equal(4, handler.Requests.Count);
		Assert.Contains("token=TOKTWO", handler.Requests[3].Query);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldUseRemoveData_WhenDeleteDataIsTrue()
	{
		var (client, handler) = CreateClient((HttpStatusCode.OK, Token("T")), (HttpStatusCode.OK, "{}"));

		await client.RemoveItemAsync("HASHXYZ", true, TestContext.Current.CancellationToken);

		Assert.Contains("action=removedata", handler.Requests[1].Query);
		Assert.Contains("hash=HASHXYZ", handler.Requests[1].Query);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenServerErrors()
	{
		var (client, _) = CreateClient((HttpStatusCode.OK, Token("T")), (HttpStatusCode.InternalServerError, "boom"));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync(TestContext.Current.CancellationToken));
	}

	private (UTorrentClient Client, StubHandler Handler) CreateClient(
		params (HttpStatusCode Status, string Body)[] responses)
	{
		var handler = new StubHandler(responses);
		var settings = new UTorrentSettings { Host = "localhost", Username = "admin", Password = "pw" };
		var client = new UTorrentClient(settings, new HttpClient(handler), new XunitLogger<UTorrentClient>(_output));

		return (client, handler);
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

		public StubHandler(params (HttpStatusCode Status, string Body)[] responses)
			=> _responses = new Queue<(HttpStatusCode, string)>(responses);

		public List<Uri> Requests { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			Requests.Add(request.RequestUri!);
			var (status, body) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();

			return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
		}
	}
}
