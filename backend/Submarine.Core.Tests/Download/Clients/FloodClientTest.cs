using System.Net;
using System.Net.Http;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class FloodClientTest
{
	private const string Magnet = "magnet:?xt=urn:btih:abcdef1234567890abcdef1234567890abcdef12&dn=Ubuntu";

	private const string TorrentsResponse = """
		{"torrents":{"HASH1":{"name":"Ubuntu","bytesDone":500,"sizeBytes":2000,"eta":120,
			"status":["downloading"],"directory":"/data","tags":["tv"]}}}
		""";

	private readonly ITestOutputHelper _output;

	public FloodClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldAuthenticateAndReturnHash_WhenReleaseIsMagnet()
	{
		var (client, handler) = CreateClient((HttpStatusCode.OK, "{}"), (HttpStatusCode.OK, "{}"));

		var id = await client.AddDownloadAsync(new ReleaseInfo { Title = "Ubuntu", Guid = "g", DownloadUrl = Magnet });

		Assert.Equal("ABCDEF1234567890ABCDEF1234567890ABCDEF12", id);
		Assert.EndsWith("/api/auth/authenticate", handler.Requests[0].AbsolutePath);
		Assert.EndsWith("/api/torrents/add-urls", handler.Requests[1].AbsolutePath);
		Assert.Contains("urn:btih", handler.Bodies[1]);
		Assert.Contains("tv", handler.Bodies[1]);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTorrent_WhenFloodReturnsTorrents()
	{
		var (client, _) = CreateClient((HttpStatusCode.OK, "{}"), (HttpStatusCode.OK, TorrentsResponse));

		var item = Assert.Single(await client.GetItemsAsync());

		Assert.Equal("HASH1", item.DownloadId);
		Assert.Equal("Ubuntu", item.Title);
		Assert.Equal(2000, item.TotalSize);
		Assert.Equal(1500, item.RemainingSize);
		Assert.Equal(TimeSpan.FromSeconds(120), item.RemainingTime);
		Assert.Equal(DownloadItemStatus.DOWNLOADING, item.Status);
		Assert.Equal("/data", item.OutputPath);
		Assert.Equal("tv", item.Category);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldReauthenticateAndRetryOnce_WhenServerReturnsUnauthorized()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, "{}"),
			(HttpStatusCode.Unauthorized, "{}"),
			(HttpStatusCode.OK, "{}"),
			(HttpStatusCode.OK, TorrentsResponse));

		await client.GetItemsAsync();

		Assert.Equal(4, handler.Requests.Count);
		Assert.EndsWith("/api/auth/authenticate", handler.Requests[2].AbsolutePath);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldPostHashesAndDeleteFlag_WhenInvoked()
	{
		var (client, handler) = CreateClient((HttpStatusCode.OK, "{}"), (HttpStatusCode.OK, "{}"));

		await client.RemoveItemAsync("HASH1", true);

		Assert.EndsWith("/api/torrents/delete", handler.Requests[1].AbsolutePath);
		Assert.Contains("HASH1", handler.Bodies[1]);
		Assert.Contains("deleteData", handler.Bodies[1]);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenAuthenticationFails()
	{
		var (client, _) = CreateClient((HttpStatusCode.Unauthorized, "{}"));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private (FloodClient Client, StubHandler Handler) CreateClient(
		params (HttpStatusCode Status, string Body)[] responses)
	{
		var handler = new StubHandler(responses);
		var settings = new FloodSettings
		{
			Host = "localhost", Username = "admin", Password = "pw", Destination = "/data", Category = "tv"
		};
		var client = new FloodClient(settings, new HttpClient(handler), new XunitLogger<FloodClient>(_output));

		return (client, handler);
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

		public StubHandler(params (HttpStatusCode Status, string Body)[] responses)
			=> _responses = new Queue<(HttpStatusCode, string)>(responses);

		public List<Uri> Requests { get; } = [];

		public List<string> Bodies { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			Requests.Add(request.RequestUri!);
			Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
			var (status, body) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();

			return new HttpResponseMessage(status) { Content = new StringContent(body) };
		}
	}
}
