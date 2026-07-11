using System.Net;
using System.Text;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class SabnzbdClientTest
{
	private const string QueueJson = """
		{
			"queue": {
				"slots": [
					{ "nzo_id": "nzo1", "filename": "Show.S01E01", "status": "Downloading", "mb": "700.00", "mbleft": "350.00", "timeleft": "0:12:34", "cat": "tv" },
					{ "nzo_id": "nzo2", "filename": "Show.S01E02", "status": "Paused", "mb": "500.00", "mbleft": "500.00", "timeleft": "0:00:00", "cat": "tv" }
				]
			}
		}
		""";

	private const string HistoryJson = """
		{
			"history": {
				"slots": [
					{ "nzo_id": "nzo3", "name": "Show.S01E00", "status": "Completed", "bytes": 734003200, "storage": "/downloads/complete/Show.S01E00", "cat": "tv", "fail_message": "" },
					{ "nzo_id": "nzo4", "name": "Show.S00E99", "status": "Failed", "bytes": 0, "storage": "", "cat": "tv", "fail_message": "Unpacking failed" }
				]
			}
		}
		""";

	private readonly ITestOutputHelper _output;

	public SabnzbdClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnNzoId_WhenSabnzbdAcceptsTheUrl()
	{
		var handler = new StubHandler(request => ModeIs(request, "addurl")
			? JsonResponse("""{ "status": true, "nzo_ids": ["SABnzbd_nzo_abc123"] }""")
			: new HttpResponseMessage(HttpStatusCode.NotFound));
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Show S01E01", Guid = "guid-1", DownloadUrl = "https://indexer.example/1.nzb", Protocol = Protocol.USENET
		};

		var id = await client.AddDownloadAsync(release);

		Assert.Equal("SABnzbd_nzo_abc123", id);
		Assert.Contains("cat=movies", handler.Requests.Single().RequestUri!.Query);
	}

	[Fact]
	public async Task AddDownloadAsync_ShouldThrowDownloadClientException_WhenSabnzbdRejectsTheUrl()
	{
		var handler = new StubHandler(request => ModeIs(request, "addurl")
			? JsonResponse("""{ "status": false, "error": "API Key Incorrect" }""")
			: new HttpResponseMessage(HttpStatusCode.NotFound));
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Show S01E01", Guid = "guid-1", DownloadUrl = "https://indexer.example/1.nzb", Protocol = Protocol.USENET
		};

		await Assert.ThrowsAsync<DownloadClientException>(() => client.AddDownloadAsync(release));
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMergeQueueAndHistory_WhenSabnzbdReturnsBoth()
	{
		var handler = new StubHandler(request =>
		{
			if (ModeIs(request, "queue") && !HasDeleteName(request)) return JsonResponse(QueueJson);
			if (ModeIs(request, "history") && !HasDeleteName(request)) return JsonResponse(HistoryJson);
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);

		var items = await client.GetItemsAsync();

		Assert.Equal(4, items.Count);

		var downloading = items.Single(item => item.DownloadId == "nzo1");
		Assert.Equal(DownloadItemStatus.DOWNLOADING, downloading.Status);
		Assert.Equal(700L * 1024 * 1024, downloading.TotalSize);
		Assert.Equal(350L * 1024 * 1024, downloading.RemainingSize);
		Assert.Equal(new TimeSpan(0, 12, 34), downloading.RemainingTime);

		Assert.Equal(DownloadItemStatus.PAUSED, items.Single(item => item.DownloadId == "nzo2").Status);

		var completed = items.Single(item => item.DownloadId == "nzo3");
		Assert.Equal(DownloadItemStatus.COMPLETED, completed.Status);
		Assert.Equal(734003200, completed.TotalSize);
		Assert.Equal("/downloads/complete/Show.S01E00", completed.OutputPath);

		var failed = items.Single(item => item.DownloadId == "nzo4");
		Assert.Equal(DownloadItemStatus.FAILED, failed.Status);
		Assert.Equal("Unpacking failed", failed.Message);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldFallBackToHistory_WhenDownloadIsNotInQueue()
	{
		var handler = new StubHandler(request =>
		{
			if (ModeIs(request, "queue") && HasDeleteName(request)) return JsonResponse("""{ "status": false }""");
			if (ModeIs(request, "history") && HasDeleteName(request)) return JsonResponse("""{ "status": true }""");
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);

		await client.RemoveItemAsync("nzo3", true);

		Assert.Equal(2, handler.Requests.Count);
		Assert.Contains("mode=history", handler.Requests[1].RequestUri!.Query);
		Assert.Contains("value=nzo3", handler.Requests[1].RequestUri!.Query);
		Assert.Contains("del_files=1", handler.Requests[1].RequestUri!.Query);
	}

	[Fact]
	public async Task TestAsync_ShouldThrowDownloadClientException_WhenSabnzbdIsUnreachable()
	{
		var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
		var client = CreateClient(handler);

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private static bool ModeIs(HttpRequestMessage request, string mode)
		=> request.RequestUri!.Query.Contains($"mode={mode}");

	private static bool HasDeleteName(HttpRequestMessage request)
		=> request.RequestUri!.Query.Contains("name=delete");

	private static HttpResponseMessage JsonResponse(string json)
		=> new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	private SabnzbdClient CreateClient(HttpMessageHandler handler)
	{
		var settings = new SabnzbdSettings { Host = "localhost", Port = 8080, ApiKey = "test-api-key", Category = "movies" };
		return new SabnzbdClient(settings, new HttpClient(handler), new XunitLogger<SabnzbdClient>(_output));
	}

	private class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

		public List<HttpRequestMessage> Requests { get; } = [];

		public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
			=> _responder = responder;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			Requests.Add(request);
			return Task.FromResult(_responder(request));
		}
	}
}
