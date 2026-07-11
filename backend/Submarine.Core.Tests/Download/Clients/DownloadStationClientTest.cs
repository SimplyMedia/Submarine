using System.Net;
using System.Net.Http;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class DownloadStationClientTest
{
	private const string ListResponse = """
		{"success":true,"data":{"tasks":[
			{"id":"dbid_1","title":"Ubuntu","size":2000,"status":"downloading",
				"additional":{"transfer":{"size_downloaded":500},"detail":{"destination":"/volume1/downloads"}}}]}}
		""";

	private static string Login(string sid)
		=> $"{{\"success\":true,\"data\":{{\"sid\":\"{sid}\"}}}}";

	private readonly ITestOutputHelper _output;

	public DownloadStationClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldLoginCreateAndResolveId_WhenReleaseHasUrl()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, Login("SID123")),
			(HttpStatusCode.OK, """{"success":true}"""),
			(HttpStatusCode.OK, ListResponse));

		var id = await client.AddDownloadAsync(new ReleaseInfo
		{
			Title = "Ubuntu", Guid = "g", DownloadUrl = "magnet:?xt=urn:btih:abc"
		});

		Assert.Equal("dbid_1", id);
		Assert.Contains("method=create", handler.Requests[1].Query);
		Assert.Contains("_sid=SID123", handler.Requests[1].Query);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTask_WhenDownloadStationReturnsTasks()
	{
		var (client, _) = CreateClient((HttpStatusCode.OK, Login("SID123")), (HttpStatusCode.OK, ListResponse));

		var item = Assert.Single(await client.GetItemsAsync());

		Assert.Equal("dbid_1", item.DownloadId);
		Assert.Equal("Ubuntu", item.Title);
		Assert.Equal(2000, item.TotalSize);
		Assert.Equal(1500, item.RemainingSize);
		Assert.Equal(DownloadItemStatus.DOWNLOADING, item.Status);
		Assert.Equal("/volume1/downloads", item.OutputPath);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldReloginAndRetryOnce_WhenSessionInvalid()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, Login("SID1")),
			(HttpStatusCode.OK, """{"success":false,"error":{"code":106}}"""),
			(HttpStatusCode.OK, Login("SID2")),
			(HttpStatusCode.OK, ListResponse));

		await client.GetItemsAsync();

		Assert.Equal(4, handler.Requests.Count);
		Assert.Contains("method=login", handler.Requests[2].Query);
		Assert.Contains("_sid=SID2", handler.Requests[3].Query);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldDeleteTask_WhenInvoked()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, Login("SID123")),
			(HttpStatusCode.OK, """{"success":true}"""));

		await client.RemoveItemAsync("dbid_1", false);

		Assert.Contains("method=delete", handler.Requests[1].Query);
		Assert.Contains("id=dbid_1", handler.Requests[1].Query);
		Assert.Contains("force_complete=false", handler.Requests[1].Query);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenInfoQueryReturnsError()
	{
		var (client, _) = CreateClient((HttpStatusCode.OK, """{"success":false,"error":{"code":100}}"""));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private (DownloadStationClient Client, StubHandler Handler) CreateClient(
		params (HttpStatusCode Status, string Body)[] responses)
	{
		var handler = new StubHandler(responses);
		var settings = new DownloadStationSettings { Host = "localhost", Username = "admin", Password = "pw" };
		var client = new DownloadStationClient(settings, new HttpClient(handler),
			new XunitLogger<DownloadStationClient>(_output));

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
