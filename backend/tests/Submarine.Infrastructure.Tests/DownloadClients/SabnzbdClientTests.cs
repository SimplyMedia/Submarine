using Shouldly;
using Xunit;
using System.Text.Json.Nodes;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class SabnzbdClientTests
{
	private static SabnzbdSettings Settings()
		=> new() { Host = "sab.local", Port = 8080, ApiKey = "key123", Category = "tv", Username = "u", Password = "p" };

	private const string QueueJson = """
		{
			"queue": {
				"slots": [
					{"nzo_id":"nzo1","filename":"Show.S01E01","mb":"1000.5","mbleft":"400.25","timeleft":"00:05:00","status":"Downloading","cat":"tv"},
					{"nzo_id":"nzo2","filename":"Paused","mb":"2000","mbleft":"2000","timeleft":"0:00:00","status":"Paused","cat":"tv"},
					{"nzo_id":"nzo3","filename":"Foreign","mb":"500","mbleft":"500","timeleft":"0:00:00","status":"Queued","cat":"other"}
				]
			}
		}
		""";

	private const string HistoryJson = """
		{
			"history": {
				"slots": [
					{"nzo_id":"nzo4","name":"Done","bytes":2000,"status":"Completed","storage":"/downloads/tv/Done","cat":"tv","fail_message":""},
					{"nzo_id":"nzo5","name":"Broken","bytes":3000,"status":"Failed","storage":"/downloads/tv/Broken","cat":"tv","fail_message":"repair failed"}
				]
			}
		}
		""";

	private static StubHttpHandler Handler(string mode, string responseJson)
		=> new((request, _) =>
			request.RequestUri!.Query.Contains($"mode={mode}") ? StubHttpHandler.Json(responseJson) : StubHttpHandler.Json("{}"));

	[Fact]
	public async Task AddAsync_ShouldAddUrlWithApiKeyCategoryAndPriority()
	{
		var handler = Handler("addurl", """{"status":true,"nzo_ids":["nzo9"]}""");
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));

		var id = await client.AddAsync(new RemoteRelease
		{
			Title = "Show.S01E01",
			DownloadUrl = "http://indexer/nzb/1",
			Size = 1000,
			Protocol = Protocol.USENET,
			Category = RemoteReleaseCategory.SERIES
		}, null, TestContext.Current.CancellationToken);

		id.ShouldBe("nzo9");
		var add = handler.Requests[0];
		add.Url.ShouldStartWith("http://sab.local:8080/api?");
		add.Url.ShouldContain("apikey=key123");
		add.Url.ShouldContain("mode=addurl");
		add.Url.ShouldContain("name=http%3A%2F%2Findexer%2Fnzb%2F1");
		add.Url.ShouldContain("cat=tv");
		add.Url.ShouldContain("priority=1");
		add.Url.ShouldContain("nzbname=Show.S01E01");
		add.HasHeader("Authorization", "Basic dTpw").ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldUploadNzbFile_WhenNzbFileProvided()
	{
		var handler = Handler("addfile", """{"status":true,"nzo_ids":["nzo7"]}""");
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));
		var release = new RemoteRelease
		{
			Title = "Show.S01E01",
			DownloadUrl = "http://indexer/nzb/1",
			NzbFile = [1, 2, 3],
			Size = 1000,
			Protocol = Protocol.USENET,
			Category = RemoteReleaseCategory.SERIES
		};

		var id = await client.AddAsync(release, null, TestContext.Current.CancellationToken);

		id.ShouldBe("nzo7");
		var add = handler.Requests[0];
		add.Method.ShouldBe(HttpMethod.Post);
		add.Body!.ShouldContain("name=name");
		add.Body!.ShouldContain("filename=Show.S01E01.nzb");
		add.Body!.ShouldContain("name=nzbname");
	}

	[Fact]
	public async Task AddAsync_ShouldThrow_WhenNoSourceAvailable()
	{
		var handler = Handler("addurl", "{}");
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));
		var release = new RemoteRelease
		{
			Title = "Show.S01E01",
			Protocol = Protocol.USENET,
			Category = RemoteReleaseCategory.SERIES
		};

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.AddAsync(release, null, TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("no nzb file");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapQueueAndHistorySlots()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.Query.Contains("mode=queue")
				? StubHttpHandler.Json(QueueJson)
				: request.RequestUri!.Query.Contains("mode=history")
					? StubHttpHandler.Json(HistoryJson)
					: StubHttpHandler.Json("{}"));
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		items[0].DownloadId.ShouldBe("nzo1");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].TotalSize.ShouldBe((long)(1000.5 * 1024 * 1024));
		items[0].RemainingSize.ShouldBe((long)(400.25 * 1024 * 1024));
		items[0].RemainingTime.ShouldBe(TimeSpan.FromMinutes(5));
		items[0].Category.ShouldBe("tv");
		items[0].IsReadOnly.ShouldBeFalse();
		items[0].Protocol.ShouldBe(Protocol.USENET);
		items[0].DownloadClientId.ShouldBe(10);
		items[0].DownloadClientName.ShouldBe("sab");
		items[1].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[2].IsReadOnly.ShouldBeTrue();
		items[3].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[3].OutputPath.ShouldBe("/downloads/tv/Done");
		items[4].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[4].Message.ShouldBe("repair failed");
	}

	[Fact]
	public async Task RemoveAsync_ShouldDeleteFromQueue_WhenFoundThere()
	{
		var handler = Handler("queue", """{"status":true}""");
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));

		await client.RemoveAsync("nzo1", true, TestContext.Current.CancellationToken);

		handler.Requests.Single().Url.ShouldContain("mode=queue");
		handler.Requests.Single().Url.ShouldContain("name=delete");
		handler.Requests.Single().Url.ShouldContain("value=nzo1");
		handler.Requests.Single().Url.ShouldContain("del_files=1");
	}

	[Fact]
	public async Task RemoveAsync_ShouldFallBackToHistory_WhenNotInQueue()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.Query.Contains("mode=queue")
				? StubHttpHandler.Json("""{"status":false}""")
				: request.RequestUri!.Query.Contains("mode=history")
					? StubHttpHandler.Json("""{"status":true}""")
					: StubHttpHandler.Json("{}"));
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));

		await client.RemoveAsync("nzo4", false, TestContext.Current.CancellationToken);

		handler.Requests.Count.ShouldBe(2);
		handler.Requests[1].Url.ShouldContain("mode=history");
	}

	[Fact]
	public async Task RemoveAsync_ShouldThrow_WhenNowhereFound()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json("""{"status":false}"""));
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.RemoveAsync("nzo1", false, TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("could not remove");
	}

	[Fact]
	public async Task TestAsync_ShouldThrowWithSabError_WhenApiKeyRejected()
	{
		var handler = Handler("version", """{"status":false,"error":"API Key incorrect"}""");
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("API Key incorrect");
	}

	[Fact]
	public async Task TestAsync_ShouldSucceed_WhenVersionReturned()
	{
		var handler = Handler("version", """{"status":true,"version":"4.1.0"}""");
		var client = new SabnzbdClient(Settings(), 10, "sab", new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);

		handler.Requests.Single().Url.ShouldContain("mode=version");
	}
}
