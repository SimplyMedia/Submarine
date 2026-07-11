using System.Net;
using System.Net.Http;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class DelugeClientTest
{
	private const string LoginOk = """{"id":1,"result":true,"error":null}""";
	private const string NotAuthenticated = """{"id":1,"result":null,"error":{"message":"Not authenticated","code":1}}""";

	private const string UpdateUi = """
		{"id":1,"error":null,"result":{"torrents":{
			"hashaaa":{"name":"Ubuntu","hash":"hashaaa","total_size":2000,"total_done":500,"eta":120,
				"state":"Downloading","progress":25.0,"save_path":"/downloads","label":"tv","message":"OK"}}}}
		""";

	private readonly ITestOutputHelper _output;

	public DelugeClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnHash_WhenReleaseIsMagnet()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, LoginOk),
			(HttpStatusCode.OK, """{"id":1,"result":"hashaaa","error":null}"""));

		var id = await client.AddDownloadAsync(new ReleaseInfo
		{
			Title = "Ubuntu", Guid = "g", DownloadUrl = "magnet:?xt=urn:btih:hashaaa"
		});

		Assert.Equal("hashaaa", id);
		Assert.Contains("core.add_torrent_magnet", handler.Bodies[1]);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTorrent_WhenDelugeReturnsUpdateUi()
	{
		var (client, _) = CreateClient((HttpStatusCode.OK, LoginOk), (HttpStatusCode.OK, UpdateUi));

		var item = Assert.Single(await client.GetItemsAsync());

		Assert.Equal("hashaaa", item.DownloadId);
		Assert.Equal("Ubuntu", item.Title);
		Assert.Equal(2000, item.TotalSize);
		Assert.Equal(1500, item.RemainingSize);
		Assert.Equal(TimeSpan.FromSeconds(120), item.RemainingTime);
		Assert.Equal(DownloadItemStatus.DOWNLOADING, item.Status);
		Assert.Equal("/downloads", item.OutputPath);
		Assert.Equal("tv", item.Category);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldReloginAndRetryOnce_WhenSessionExpired()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, LoginOk),
			(HttpStatusCode.OK, NotAuthenticated),
			(HttpStatusCode.OK, LoginOk),
			(HttpStatusCode.OK, UpdateUi));

		await client.GetItemsAsync();

		Assert.Equal(4, handler.Bodies.Count);
		Assert.Contains("auth.login", handler.Bodies[2]);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldPassRemoveDataFlag_WhenInvoked()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, LoginOk),
			(HttpStatusCode.OK, """{"id":1,"result":true,"error":null}"""));

		await client.RemoveItemAsync("hashaaa", true);

		Assert.Contains("core.remove_torrent", handler.Bodies[1]);
		Assert.Contains("true", handler.Bodies[1]);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenAuthenticationFails()
	{
		var (client, _) = CreateClient((HttpStatusCode.OK, """{"id":1,"result":false,"error":null}"""));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private (DelugeClient Client, StubHandler Handler) CreateClient(
		params (HttpStatusCode Status, string Body)[] responses)
	{
		var handler = new StubHandler(responses);
		var settings = new DelugeSettings { Host = "localhost", Password = "secret" };
		var client = new DelugeClient(settings, new HttpClient(handler), new XunitLogger<DelugeClient>(_output));

		return (client, handler);
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

		public StubHandler(params (HttpStatusCode Status, string Body)[] responses)
			=> _responses = new Queue<(HttpStatusCode, string)>(responses);

		public List<string> Bodies { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
			var (status, body) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();

			return new HttpResponseMessage(status) { Content = new StringContent(body) };
		}
	}
}
