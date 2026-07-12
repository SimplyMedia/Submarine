using System.Net;
using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class TransmissionClientTest
{
	private const string TorrentGetJson = """
		{
			"result": "success",
			"arguments": {
				"torrents": [
					{ "id": 1, "hashString": "hash1", "name": "Movie.1", "totalSize": 1000, "leftUntilDone": 500, "eta": 300, "status": 4, "downloadDir": "/downloads", "errorString": "" },
					{ "id": 2, "hashString": "hash2", "name": "Movie.2", "totalSize": 2000, "leftUntilDone": 0, "eta": -1, "status": 6, "downloadDir": "/downloads", "errorString": "" },
					{ "id": 3, "hashString": "hash3", "name": "Movie.3", "totalSize": 1500, "leftUntilDone": 1500, "eta": -1, "status": 0, "downloadDir": "/downloads", "errorString": "" },
					{ "id": 4, "hashString": "hash4", "name": "Movie.4", "totalSize": 500, "leftUntilDone": 500, "eta": -1, "status": 4, "downloadDir": "/downloads", "errorString": "Tracker gave an error" },
					{ "id": 5, "hashString": "hash5", "name": "Movie.5", "totalSize": 1000, "leftUntilDone": 0, "eta": -1, "status": 0, "downloadDir": "/downloads", "errorString": "" }
				]
			}
		}
		""";

	private readonly ITestOutputHelper _output;

	public TransmissionClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnHashString_WhenTransmissionAddsTheTorrent()
	{
		var handler = new StubHandler((request, body) => RouteRequest(request, body, method => method switch
		{
			"torrent-add" => JsonResponse("""
				{ "result": "success", "arguments": { "torrent-added": { "id": 1, "hashString": "abcdef0123456789" } } }
				"""),
			_ => new HttpResponseMessage(HttpStatusCode.NotFound)
		}));
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Movie 1",
			Guid = "guid-1",
			DownloadUrl = "https://indexer.example/download/1.torrent",
			Protocol = Protocol.BITTORRENT
		};

		var id = await client.AddDownloadAsync(release);

		Assert.Equal("abcdef0123456789", id);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapStatusesAndSizes_WhenTransmissionReturnsTorrents()
	{
		var handler = new StubHandler((request, body) => RouteRequest(request, body,
			method => method == "torrent-get" ? JsonResponse(TorrentGetJson) : new HttpResponseMessage(HttpStatusCode.NotFound)));
		var client = CreateClient(handler);

		var items = await client.GetItemsAsync();

		Assert.Equal(5, items.Count);

		var downloading = items.Single(item => item.DownloadId == "hash1");
		Assert.Equal(DownloadItemStatus.DOWNLOADING, downloading.Status);
		Assert.Equal(1000, downloading.TotalSize);
		Assert.Equal(500, downloading.RemainingSize);
		Assert.Equal(TimeSpan.FromSeconds(300), downloading.RemainingTime);

		Assert.Equal(DownloadItemStatus.COMPLETED, items.Single(item => item.DownloadId == "hash2").Status);
		Assert.Equal(DownloadItemStatus.PAUSED, items.Single(item => item.DownloadId == "hash3").Status);
		Assert.Equal(DownloadItemStatus.COMPLETED, items.Single(item => item.DownloadId == "hash5").Status);

		var failed = items.Single(item => item.DownloadId == "hash4");
		Assert.Equal(DownloadItemStatus.FAILED, failed.Status);
		Assert.Equal("Tracker gave an error", failed.Message);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldHandshakeSessionId_WhenServerReturns409()
	{
		var conflictResponses = 0;
		var handler = new StubHandler((request, body) =>
		{
			if (!request.Headers.Contains("X-Transmission-Session-Id"))
			{
				conflictResponses++;
				var conflict = new HttpResponseMessage(HttpStatusCode.Conflict);
				conflict.Headers.TryAddWithoutValidation("X-Transmission-Session-Id", "test-session-id");
				return conflict;
			}

			Assert.Equal("test-session-id", request.Headers.GetValues("X-Transmission-Session-Id").Single());
			return JsonResponse(TorrentGetJson);
		});
		var client = CreateClient(handler);

		var items = await client.GetItemsAsync();

		Assert.Equal(1, conflictResponses);
		Assert.Equal(5, items.Count);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldSendIdsAndDeleteLocalData_WhenCalled()
	{
		var handler = new StubHandler((request, body) => RouteRequest(request, body, method =>
		{
			if (method != "torrent-remove") return new HttpResponseMessage(HttpStatusCode.NotFound);

			using var doc = JsonDocument.Parse(body);
			var arguments = doc.RootElement.GetProperty("arguments");
			Assert.Equal("hash1", arguments.GetProperty("ids")[0].GetString());
			Assert.True(arguments.GetProperty("delete-local-data").GetBoolean());

			return JsonResponse("""{ "result": "success", "arguments": {} }""");
		}));
		var client = CreateClient(handler);

		await client.RemoveItemAsync("hash1", true);
	}

	[Fact]
	public async Task TestAsync_ShouldThrowDownloadClientException_WhenSessionGetFails()
	{
		var handler = new StubHandler((request, body) => RouteRequest(request, body, method => method == "session-get"
			? JsonResponse("""{ "result": "invalid session", "arguments": {} }""")
			: new HttpResponseMessage(HttpStatusCode.NotFound)));
		var client = CreateClient(handler);

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private static HttpResponseMessage RouteRequest(HttpRequestMessage request, string body,
		Func<string, HttpResponseMessage> methodHandler)
	{
		if (!request.Headers.Contains("X-Transmission-Session-Id"))
		{
			var conflict = new HttpResponseMessage(HttpStatusCode.Conflict);
			conflict.Headers.TryAddWithoutValidation("X-Transmission-Session-Id", "test-session-id");
			return conflict;
		}

		using var doc = JsonDocument.Parse(body);
		var method = doc.RootElement.GetProperty("method").GetString()!;
		return methodHandler(method);
	}

	private static HttpResponseMessage JsonResponse(string json)
		=> new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	private TransmissionClient CreateClient(HttpMessageHandler handler)
	{
		var settings = new TransmissionSettings { Host = "localhost", Port = 9091, Username = "admin", Password = "password" };
		return new TransmissionClient(settings, new HttpClient(handler), new XunitLogger<TransmissionClient>(_output));
	}

	private class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responder;

		public StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
			=> _responder = responder;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
			return _responder(request, body);
		}
	}
}
