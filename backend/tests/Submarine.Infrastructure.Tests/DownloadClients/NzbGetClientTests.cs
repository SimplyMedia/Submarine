using System.Net;
using Shouldly;
using Xunit;
using System.Text;
using System.Text.Json.Nodes;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class NzbGetClientTests
{
	private static NzbGetSettings Settings()
		=> new() { Host = "nzbget.local", Port = 6789, Username = "user", Password = "pass", Category = "tv" };

	private const string ListGroupsJson = """
		[
			{"NZBID":1,"NZBName":"Show.S01E01","FileSizeLo":1000,"FileSizeHi":0,"RemainingSizeLo":400,"RemainingSizeHi":0,"Status":"DOWNLOADING","DestDir":"/downloads/tv","Category":"tv"},
			{"NZBID":2,"NZBName":"Paused","FileSizeLo":2000,"FileSizeHi":0,"RemainingSizeLo":2000,"RemainingSizeHi":0,"Status":"PAUSED","DestDir":"/downloads/tv","Category":"tv"},
			{"NZBID":3,"NZBName":"Foreign","FileSizeLo":500,"FileSizeHi":0,"RemainingSizeLo":500,"RemainingSizeHi":0,"Status":"QUEUED","DestDir":"/downloads/tv","Category":"other"}
		]
		""";

	private const string HistoryJson = """
		[
			{"NZBID":4,"Name":"Done","FileSizeLo":2000,"FileSizeHi":0,"Status":"SUCCESS/ALL","DestDir":"/downloads/tv/Done","Category":"tv"},
			{"NZBID":5,"Name":"Broken","FileSizeLo":3000,"FileSizeHi":0,"Status":"FAILURE/REPAIR","DestDir":"/downloads/tv/Broken","Category":"tv"}
		]
		""";

	[Fact]
	public async Task AddAsync_ShouldAppendBase64NzbWithCategoryPriorityAndPaused()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return StubHttpHandler.Json("""{"id":1,"result":42,"error":null}""");
		});
		var client = new NzbGetClient(Settings() with { AddPaused = true }, 11, "nzbget", new HttpClient(handler));
		var release = new RemoteRelease
		{
			Title = "Show.S01E01",
			DownloadUrl = "http://indexer/nzb/1",
			NzbFile = [9, 8, 7],
			Size = 1000,
			Protocol = Protocol.USENET,
			Category = RemoteReleaseCategory.SERIES
		};

		var id = await client.AddAsync(release, null, TestContext.Current.CancellationToken);

		id.ShouldBe("42");
		var payload = JsonNode.Parse(requestBody!)!;
		payload["method"]!.GetValue<string>().ShouldBe("append");
		var parameters = payload["params"]!.AsArray();
		parameters[0]!.GetValue<string>().ShouldBe("Show.S01E01.nzb");
		parameters[1]!.GetValue<string>().ShouldBe(Convert.ToBase64String([9, 8, 7]));
		parameters[2]!.GetValue<string>().ShouldBe("tv");
		parameters[3]!.GetValue<int>().ShouldBe(10); // HIGH maps to NZBGet 10
		parameters[4]!.GetValue<bool>().ShouldBeFalse();
		parameters[5]!.GetValue<bool>().ShouldBeTrue();
		handler.Requests[0].Url.ShouldBe("http://nzbget.local:6789/jsonrpc");
		handler.Requests[0].HasHeader("Authorization", "Basic dXNlcjpwYXNz").ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldFetchNzbFromUrl_WhenNzbFileMissing()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.IsAbsoluteUri && request.RequestUri!.Host == "indexer"
				? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }
				: StubHttpHandler.Json("""{"id":1,"result":7,"error":null}"""));
		var client = new NzbGetClient(Settings(), 11, "nzbget", new HttpClient(handler));
		var release = new RemoteRelease
		{
			Title = "Show.S01E01",
			DownloadUrl = "http://indexer/nzb/1",
			Size = 1000,
			Protocol = Protocol.USENET,
			Category = RemoteReleaseCategory.SERIES
		};

		var id = await client.AddAsync(release, null, TestContext.Current.CancellationToken);

		id.ShouldBe("7");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapGroupsAndHistoryWithIdentity()
	{
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonNode.Parse(body!)!["method"]!.GetValue<string>();
			return StubHttpHandler.Json($$"""{"id":1,"result":{{(method == "listgroups" ? ListGroupsJson : HistoryJson)}},"error":null}""");
		});
		var client = new NzbGetClient(Settings(), 11, "nzbget", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		items[0].DownloadId.ShouldBe("1");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].TotalSize.ShouldBe(1000);
		items[0].RemainingSize.ShouldBe(400);
		items[0].OutputPath.ShouldBe("/downloads/tv");
		items[0].Category.ShouldBe("tv");
		items[0].IsReadOnly.ShouldBeFalse();
		items[0].Protocol.ShouldBe(Protocol.USENET);
		items[0].DownloadClientId.ShouldBe(11);
		items[0].DownloadClientName.ShouldBe("nzbget");
		items[1].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[2].IsReadOnly.ShouldBeTrue();
		items[3].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[3].OutputPath.ShouldBe("/downloads/tv/Done");
		items[4].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[4].Message.ShouldBe("FAILURE/REPAIR");
	}

	[Fact]
	public async Task RemoveAsync_ShouldEditQueue_WhenFoundThere()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return StubHttpHandler.Json("""{"id":1,"result":true,"error":null}""");
		});
		var client = new NzbGetClient(Settings(), 11, "nzbget", new HttpClient(handler));

		await client.RemoveAsync("1", true, TestContext.Current.CancellationToken);

		var payload = JsonNode.Parse(requestBody!)!;
		payload["method"]!.GetValue<string>().ShouldBe("editqueue");
		payload["params"]![0]!.GetValue<string>().ShouldBe("GroupFinalDelete");
		payload["params"]![3]!.AsArray()[0]!.GetValue<int>().ShouldBe(1);
	}

	[Fact]
	public async Task RemoveAsync_ShouldFallBackToHistoryDelete_WhenNotInQueue()
	{
		var bodies = new List<string>();
		var handler = new StubHttpHandler((_, body) =>
		{
			bodies.Add(body!);
			var method = JsonNode.Parse(body!)!["method"]!.GetValue<string>();
			var result = method == "editqueue" ? "false" : "true";
			return StubHttpHandler.Json($"{{\"id\":1,\"result\":{result},\"error\":null}}");
		});
		var client = new NzbGetClient(Settings(), 11, "nzbget", new HttpClient(handler));

		await client.RemoveAsync("4", false, TestContext.Current.CancellationToken);

		bodies.Count.ShouldBe(2);
		var historyDelete = JsonNode.Parse(bodies[1])!;
		historyDelete["method"]!.GetValue<string>().ShouldBe("historydelete");
	}

	[Fact]
	public async Task RemoveAsync_ShouldThrow_WhenIdInvalid()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json("{}"));
		var client = new NzbGetClient(Settings(), 11, "nzbget", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.RemoveAsync("not-a-number", false, TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("invalid download id");
	}

	[Fact]
	public async Task TestAsync_ShouldThrowWithRpcMessage_WhenUnauthorized()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json(
			"""{"id":1,"result":null,"error":{"message":"Unauthorized"}}"""));
		var client = new NzbGetClient(Settings(), 11, "nzbget", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("Unauthorized");
	}
}
