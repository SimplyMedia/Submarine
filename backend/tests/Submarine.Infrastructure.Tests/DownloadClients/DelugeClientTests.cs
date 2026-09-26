using Shouldly;
using Xunit;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class DelugeClientTests
{
	private static DelugeSettings Settings()
		=> new() { Host = "deluge.local", Port = 8112, Password = "secret", Category = "tv" };

	private static HttpResponseMessage Rpc(object? result, bool setCookie = false)
	{
		var response = StubHttpHandler.Json(
			JsonSerializer.Serialize(new { id = 1, result, error = (object?)null }));
		if (setCookie)
			response.Headers.Add("Set-Cookie", "_session_id=deluge-sid; expires=Thu, 01 Jan 2099 00:00:00 GMT; path=/");

		return response;
	}

	[Fact]
	public async Task AddAsync_ShouldLoginAndAddMagnetWithCookie()
	{
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			if (method == "auth.login")
				return Rpc(true, setCookie: true);
			if (method == "core.add_torrent_magnet")
				return Rpc("0123456789abcdef0123456789abcdef01234567");

			return Rpc(null);
		});
		var client = new DelugeClient(Settings(), 4, "deluge", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("0123456789abcdef0123456789abcdef01234567");
		handler.Requests.Count.ShouldBe(3); // login, add, label
		handler.Requests[1].HasHeader("Cookie", "_session_id=deluge-sid").ShouldBeTrue();
		handler.Requests[1].Body!.ShouldContain("\"method\":\"core.add_torrent_magnet\"");
	}

	[Fact]
	public async Task AddAsync_ShouldUploadTorrentFileBase64_WhenTorrentRelease()
	{
		string? addBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			if (method == "auth.login")
				return Rpc(true, setCookie: true);
			if (method == "core.add_torrent_file")
			{
				addBody = body;
				return Rpc("hash123");
			}

			return Rpc(null);
		});
		var client = new DelugeClient(Settings(), 4, "deluge", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("hash123");
		addBody!.ShouldContain("\"method\":\"core.add_torrent_file\"");
		addBody!.ShouldContain(Convert.ToBase64String(TestTorrent.Bytes));
	}

	[Fact]
	public async Task AddAsync_ShouldApplyPausedAndRatioOptions_WhenConfigured()
	{
		string? addBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			if (method == "auth.login")
				return Rpc(true, setCookie: true);
			if (method == "core.add_torrent_magnet")
			{
				addBody = body;
				return Rpc("hash");
			}

			return Rpc(null);
		});
		var client = new DelugeClient(Settings() with { AddPaused = true }, 4, "deluge", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), new SeedCriteria(1.5, null, null), TestContext.Current.CancellationToken);

		addBody!.ShouldContain("\"add_paused\":true");
		addBody!.ShouldContain("\"stop_at_ratio\":true");
		addBody!.ShouldContain("\"stop_ratio\":1.5");
	}

	[Fact]
	public async Task AddAsync_ShouldIgnoreLabelFailure_WhenLabelPluginMissing()
	{
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			if (method == "auth.login")
				return Rpc(true, setCookie: true);
			if (method == "label.set_torrent")
				return StubHttpHandler.Json("""{"id":1,"result":null,"error":{"message":"Unknown method"}}""");

			return Rpc("hash");
		});
		var client = new DelugeClient(Settings(), 4, "deluge", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("hash");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTorrentStatesAndIdentity()
	{
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			if (method == "auth.login")
				return Rpc(true, setCookie: true);
			if (method == "core.get_torrents_status")
			{
				return StubHttpHandler.Json("""
					{"id":1,"result":{"aaaa":{"name":"Show.S01E01","hash":"aaaa","total_size":1000,"total_done":600,"eta":30,"state":"Downloading","save_path":"/data/tv","label":"tv","message":""},"bbbb":{"name":"Seeding","hash":"bbbb","total_size":2000,"total_done":2000,"eta":-1,"state":"Seeding","save_path":"/data/tv","label":"tv","message":""},"cccc":{"name":"Foreign","hash":"cccc","total_size":3000,"total_done":0,"eta":-1,"state":"Error","save_path":"/data/tv","label":"other","message":"tracker error"}},"error":null}
					""");
			}

			return Rpc(null);
		});
		var client = new DelugeClient(Settings(), 4, "deluge", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(3);
		items[0].DownloadId.ShouldBe("aaaa");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].RemainingSize.ShouldBe(400);
		items[0].RemainingTime.ShouldBe(TimeSpan.FromSeconds(30));
		items[0].OutputPath.ShouldBe("/data/tv");
		items[0].Category.ShouldBe("tv");
		items[0].IsReadOnly.ShouldBeFalse();
		items[0].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[0].DownloadClientId.ShouldBe(4);
		items[0].DownloadClientName.ShouldBe("deluge");
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[2].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[2].IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public async Task RemoveAsync_ShouldCallRemoveTorrentWithDeleteData()
	{
		string? removeBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			if (method == "auth.login")
				return Rpc(true, setCookie: true);
			if (method == "core.remove_torrent")
			{
				removeBody = body;
				return Rpc(true);
			}

			return Rpc(null);
		});
		var client = new DelugeClient(Settings(), 4, "deluge", new HttpClient(handler));

		await client.RemoveAsync("aaaa", true, TestContext.Current.CancellationToken);

		removeBody!.ShouldContain("\"method\":\"core.remove_torrent\"");
		removeBody!.ShouldContain("\"aaaa\"");
		removeBody!.ShouldContain("true");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenLoginRejected()
	{
		var handler = new StubHttpHandler((_, _) => Rpc(false));
		var client = new DelugeClient(Settings(), 4, "deluge", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("authentication failed");
	}

	[Fact]
	public async Task Rpc_ShouldReLogin_WhenSessionExpired()
	{
		var logins = 0;
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			if (method == "auth.login")
			{
				logins++;
				return Rpc(true, setCookie: true);
			}

			return logins == 1
				? StubHttpHandler.Json("""{"id":1,"result":null,"error":{"message":"Not authenticated"}}""")
				: Rpc("1.2");
		});
		var client = new DelugeClient(Settings(), 4, "deluge", new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);

		logins.ShouldBe(2);
	}
}
