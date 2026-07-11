using System.Net;
using System.Net.Http;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class Aria2ClientTest
{
	private const string ActiveResponse = """
		{"id":"x","jsonrpc":"2.0","result":[
			{"gid":"g1","status":"active","totalLength":"2000","completedLength":"500","dir":"/downloads",
				"files":[{"path":"/downloads/Ubuntu.iso"}]}]}
		""";

	private const string EmptyResponse = """{"id":"x","jsonrpc":"2.0","result":[]}""";

	private const string StoppedResponse = """
		{"id":"x","jsonrpc":"2.0","result":[
			{"gid":"g2","status":"complete","totalLength":"1000","completedLength":"1000","dir":"/done",
				"files":[{"path":"/done/x.iso"}]}]}
		""";

	private readonly ITestOutputHelper _output;

	public Aria2ClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnGidAndSendToken_WhenReleaseHasUrl()
	{
		var (client, handler) = CreateClient((HttpStatusCode.OK, """{"id":"x","jsonrpc":"2.0","result":"gid123"}"""));

		var id = await client.AddDownloadAsync(new ReleaseInfo
		{
			Title = "Ubuntu", Guid = "g", DownloadUrl = "https://tracker/ubuntu.torrent"
		});

		Assert.Equal("gid123", id);
		Assert.Contains("aria2.addUri", handler.Bodies[0]);
		Assert.Contains("token:s3cr3t", handler.Bodies[0]);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMergeAndMapDownloads_WhenAria2ReturnsAllStates()
	{
		var (client, _) = CreateClient(
			(HttpStatusCode.OK, ActiveResponse),
			(HttpStatusCode.OK, EmptyResponse),
			(HttpStatusCode.OK, StoppedResponse));

		var items = await client.GetItemsAsync();

		Assert.Equal(2, items.Count);

		var active = items[0];
		Assert.Equal("g1", active.DownloadId);
		Assert.Equal("Ubuntu.iso", active.Title);
		Assert.Equal(2000, active.TotalSize);
		Assert.Equal(1500, active.RemainingSize);
		Assert.Equal(DownloadItemStatus.DOWNLOADING, active.Status);
		Assert.Equal("/downloads", active.OutputPath);

		Assert.Equal(DownloadItemStatus.COMPLETED, items[1].Status);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldRemoveAndClearResult_WhenInvoked()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, """{"id":"x","jsonrpc":"2.0","result":"OK"}"""),
			(HttpStatusCode.OK, """{"id":"x","jsonrpc":"2.0","result":"OK"}"""));

		await client.RemoveItemAsync("gid123", false);

		Assert.Contains("aria2.remove", handler.Bodies[0]);
		Assert.Contains("gid123", handler.Bodies[0]);
		Assert.Contains("aria2.removeDownloadResult", handler.Bodies[1]);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenServerReturnsError()
	{
		var (client, _) = CreateClient(
			(HttpStatusCode.OK, """{"id":"x","jsonrpc":"2.0","error":{"code":1,"message":"Unauthorized"}}"""));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private (Aria2Client Client, StubHandler Handler) CreateClient(
		params (HttpStatusCode Status, string Body)[] responses)
	{
		var handler = new StubHandler(responses);
		var settings = new Aria2Settings { Host = "localhost", Secret = "s3cr3t" };
		var client = new Aria2Client(settings, new HttpClient(handler), new XunitLogger<Aria2Client>(_output));

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
