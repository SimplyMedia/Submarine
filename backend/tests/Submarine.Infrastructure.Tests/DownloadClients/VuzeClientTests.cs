using Shouldly;
using Xunit;
using System.Text.Json.Nodes;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class VuzeClientTests
{
	private static TransmissionSettings Settings()
		=> new() { Host = "vuze.local", Port = 9091, Category = "tv" };

	private static HttpResponseMessage Success(JsonObject? arguments = null)
		=> StubHttpHandler.Json(new JsonObject
		{
			["result"] = "success",
			["arguments"] = arguments ?? new JsonObject()
		}.ToJsonString());

	[Fact]
	public void Type_ShouldBeVuze()
	{
		var client = new VuzeClient(Settings(), 1, "vuze", new HttpClient());

		client.Type.ShouldBe(DownloadClientType.VUZE);
		client.Protocol.ShouldBe(Protocol.BITTORRENT);
	}

	[Fact]
	public async Task AddAsync_ShouldSendMagnetWithoutLabels_BecauseVuzeDoesNotSupportLabels()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Success(new JsonObject
			{
				["torrent-added"] = new JsonObject { ["id"] = 5, ["hashString"] = "abcd" }
			});
		});
		var client = new VuzeClient(Settings(), 1, "vuze", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		captured!["method"]!.GetValue<string>().ShouldBe("torrent-add");
		captured["arguments"]!.AsObject().ContainsKey("labels").ShouldBeFalse();
	}

	[Fact]
	public async Task GetItemsAsync_ShouldAppendTorrentName_WhenSingleFileTorrentReportsRootFolder()
	{
		var handler = new StubHttpHandler((_, _) => Success(new JsonObject
		{
			["torrents"] = new JsonArray(
				new JsonObject
				{
					["id"] = 1, ["hashString"] = "aaa", ["name"] = "movie.mkv", ["totalSize"] = 1000,
					["leftUntilDone"] = 400, ["eta"] = 30, ["status"] = 4, ["downloadDir"] = "/data/downloads",
					["errorString"] = "", ["files"] = new JsonArray(new JsonObject())
				},
				new JsonObject
				{
					["id"] = 2, ["hashString"] = "bbb", ["name"] = "Show.Season.01", ["totalSize"] = 2000,
					["leftUntilDone"] = 0, ["eta"] = -1, ["status"] = 0, ["downloadDir"] = "/data/downloads/Show.Season.01",
					["errorString"] = "", ["files"] = new JsonArray(new JsonObject(), new JsonObject())
				})
		}));
		var client = new VuzeClient(Settings(), 1, "vuze", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items[0].OutputPath.ShouldBe("/data/downloads/movie.mkv");
		items[1].OutputPath.ShouldBe("/data/downloads/Show.Season.01");
		items[0].IsReadOnly.ShouldBeFalse();
	}

	[Fact]
	public async Task RemoveAsync_ShouldRemoveWithLocalData_WhenDeleteData()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Success();
		});
		var client = new VuzeClient(Settings(), 1, "vuze", new HttpClient(handler));

		await client.RemoveAsync("aaa", true, TestContext.Current.CancellationToken);

		captured!["method"]!.GetValue<string>().ShouldBe("torrent-remove");
		captured["arguments"]!["delete-local-data"]!.GetValue<bool>().ShouldBeTrue();
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenProtocolVersionBelowMinimum()
	{
		var handler = new StubHttpHandler((_, _) =>
			Success(new JsonObject { ["rpc-version"] = 13 }));
		var client = new VuzeClient(Settings(), 1, "vuze", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("14");
	}

	[Fact]
	public async Task TestAsync_ShouldSucceed_WhenProtocolVersionMeetsMinimum()
	{
		var handler = new StubHttpHandler((_, _) =>
			Success(new JsonObject { ["rpc-version"] = 17 }));
		var client = new VuzeClient(Settings(), 1, "vuze", new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);
	}
}
