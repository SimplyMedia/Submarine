using System.Net;
using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class NzbGetClientTest
{
	private const string ListGroupsJson = """
		{
			"result": [
				{ "NZBID": 101, "NZBName": "Show.S01E01", "FileSizeLo": 734003200, "FileSizeHi": 0, "RemainingSizeLo": 200000000, "RemainingSizeHi": 0, "Status": "DOWNLOADING", "DestDir": "/downloads/inter/Show.S01E01", "Category": "tv" },
				{ "NZBID": 102, "NZBName": "Show.S01E02", "FileSizeLo": 500000000, "FileSizeHi": 0, "RemainingSizeLo": 500000000, "RemainingSizeHi": 0, "Status": "PAUSED", "DestDir": "/downloads/inter/Show.S01E02", "Category": "tv" }
			]
		}
		""";

	private const string HistoryJson = """
		{
			"result": [
				{ "NZBID": 103, "Name": "Show.S01E00", "FileSizeLo": 734003200, "FileSizeHi": 0, "Status": "SUCCESS/ALL", "DestDir": "/downloads/complete/Show.S01E00", "Category": "tv" },
				{ "NZBID": 104, "Name": "Show.S00E99", "FileSizeLo": 0, "FileSizeHi": 0, "Status": "FAILURE/UNPACK", "DestDir": "/downloads/complete/Show.S00E99", "Category": "tv" }
			]
		}
		""";

	private readonly ITestOutputHelper _output;

	public NzbGetClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnNzbId_WhenNzbgetAcceptsTheUrl()
	{
		var handler = new StubHandler((request, body) => ExtractMethod(body) == "append"
			? JsonResponse("""{ "result": 55 }""")
			: new HttpResponseMessage(HttpStatusCode.NotFound));
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Show S01E01", Guid = "guid-1", DownloadUrl = "https://indexer.example/1.nzb", Protocol = Protocol.USENET
		};

		var id = await client.AddDownloadAsync(release, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal("55", id);
		var authHeader = handler.Calls.Single().Request.Headers.Authorization;
		Assert.NotNull(authHeader);
		Assert.Equal("Basic", authHeader!.Scheme);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMergeGroupsAndHistory_WhenNzbgetReturnsBoth()
	{
		var handler = new StubHandler((request, body) => ExtractMethod(body) switch
		{
			"listgroups" => JsonResponse(ListGroupsJson),
			"history" => JsonResponse(HistoryJson),
			_ => new HttpResponseMessage(HttpStatusCode.NotFound)
		});
		var client = CreateClient(handler);

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		Assert.Equal(4, items.Count);

		var downloading = items.Single(item => item.DownloadId == "101");
		Assert.Equal(DownloadItemStatus.DOWNLOADING, downloading.Status);
		Assert.Equal(734003200, downloading.TotalSize);
		Assert.Equal(200000000, downloading.RemainingSize);
		Assert.Equal("/downloads/inter/Show.S01E01", downloading.OutputPath);

		Assert.Equal(DownloadItemStatus.PAUSED, items.Single(item => item.DownloadId == "102").Status);

		var completed = items.Single(item => item.DownloadId == "103");
		Assert.Equal(DownloadItemStatus.COMPLETED, completed.Status);
		Assert.Equal(734003200, completed.TotalSize);
		Assert.Equal("/downloads/complete/Show.S01E00", completed.OutputPath);

		Assert.Equal(DownloadItemStatus.FAILED, items.Single(item => item.DownloadId == "104").Status);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldSendGroupFinalDelete_WhenDownloadIsQueued()
	{
		var handler = new StubHandler((request, body) =>
		{
			if (ExtractMethod(body) != "editqueue") return new HttpResponseMessage(HttpStatusCode.NotFound);

			using var doc = JsonDocument.Parse(body);
			var parameters = doc.RootElement.GetProperty("params");
			Assert.Equal("GroupFinalDelete", parameters[0].GetString());
			Assert.Equal(101, parameters[3][0].GetInt32());

			return JsonResponse("""{ "result": true }""");
		});
		var client = CreateClient(handler);

		await client.RemoveItemAsync("101", true, TestContext.Current.CancellationToken);

		Assert.Single(handler.Calls);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldFallBackToHistoryDelete_WhenDownloadIsNotQueued()
	{
		var handler = new StubHandler((request, body) => ExtractMethod(body) switch
		{
			"editqueue" => JsonResponse("""{ "result": false }"""),
			"historydelete" => JsonResponse("""{ "result": true }"""),
			_ => new HttpResponseMessage(HttpStatusCode.NotFound)
		});
		var client = CreateClient(handler);

		await client.RemoveItemAsync("103", true, TestContext.Current.CancellationToken);

		Assert.Equal(2, handler.Calls.Count);
	}

	[Fact]
	public async Task TestAsync_ShouldThrowDownloadClientException_WhenNzbgetReturnsAnError()
	{
		var handler = new StubHandler((_, _) => JsonResponse("""{ "error": { "code": 1, "message": "Access denied" } }"""));
		var client = CreateClient(handler);

		var ex = await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync(TestContext.Current.CancellationToken));
		Assert.Contains("Access denied", ex.Message);
	}

	private static string ExtractMethod(string body)
	{
		using var doc = JsonDocument.Parse(body);
		return doc.RootElement.GetProperty("method").GetString()!;
	}

	private static HttpResponseMessage JsonResponse(string json)
		=> new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	private NzbGetClient CreateClient(HttpMessageHandler handler)
	{
		var settings = new NzbGetSettings
		{
			Host = "localhost", Port = 6789, Username = "nzbget", Password = "tegbzn6789", Category = "tv"
		};

		return new NzbGetClient(settings, new HttpClient(handler), new XunitLogger<NzbGetClient>(_output));
	}

	private class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responder;

		public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

		public StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
			=> _responder = responder;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
			Calls.Add((request, body));
			return _responder(request, body);
		}
	}
}
