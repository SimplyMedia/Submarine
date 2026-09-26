using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Api.Features.Blocklist;
using Submarine.Api.Features.DownloadClients;
using Submarine.Api.Features.EpisodeFiles;
using Submarine.Api.Features.History;
using Submarine.Api.Features.Queue;
using Submarine.Core.Commands;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Downloads;
using Xunit;

namespace Submarine.Api.IntegrationTests.Downloads;

public sealed class DownloadClientCrudAndQueueApiTests : IClassFixture<DownloadsApiFactory>
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly DownloadsApiFactory _factory;

	public DownloadClientCrudAndQueueApiTests(DownloadsApiFactory factory) => _factory = factory;

	[Fact]
	public async Task DownloadClient_FullCrudPlusSchemaAndTest_ShouldRoundTrip()
	{
		var client = await _factory.CreateAuthorizedClientAsync();
		var torrentFolder = Directory.CreateTempSubdirectory("submarine-it-torrent-").FullName;
		var watchFolder = Directory.CreateTempSubdirectory("submarine-it-watch-").FullName;

		var schema = await client.GetAsync("/api/v1/download-clients/schema");
		schema.StatusCode.ShouldBe(HttpStatusCode.OK);
		var schemas = await schema.Content.ReadFromJsonAsync<List<DownloadClientSchema>>(JsonOptions);
		schemas!.ShouldContain(x => x.Type == DownloadClientType.TORRENT_BLACKHOLE);

		var settings = JsonSerializer.SerializeToElement(new
		{
			torrentFolder,
			watchFolder,
			saveMagnetFiles = true,
			magnetFileExtension = ".magnet",
			readOnly = false
		});

		var create = await client.PostAsJsonAsync("/api/v1/download-clients", new
		{
			name = "Blackhole",
			type = "TORRENT_BLACKHOLE",
			enable = true,
			priority = 1,
			settings,
			removeCompleted = false,
			removeFailed = false,
			tagIds = (List<int>?)null
		});
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<DownloadClientDto>(JsonOptions);
		created!.Name.ShouldBe("Blackhole");
		// The columns have a database default of true; an explicit false must still be stored.
		var stored = await _factory.WithDbAsync(db => db.DownloadClients.SingleAsync(c => c.Id == created.Id));
		stored.RemoveCompleted.ShouldBeFalse();
		stored.RemoveFailed.ShouldBeFalse();

		var test = await client.PostAsync($"/api/v1/download-clients/{created.Id}/test", null);
		test.StatusCode.ShouldBe(HttpStatusCode.OK);
		var testResult = await test.Content.ReadFromJsonAsync<DownloadClientTestResult>();
		testResult!.IsValid.ShouldBeTrue(testResult.Message);

		var update = await client.PutAsJsonAsync($"/api/v1/download-clients/{created.Id}", new
		{
			name = "Blackhole Renamed",
			type = "TORRENT_BLACKHOLE",
			enable = false,
			priority = 2,
			settings,
			removeCompleted = false,
			removeFailed = false,
			tagIds = (List<int>?)null
		});
		update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await update.Content.ReadFromJsonAsync<DownloadClientDto>(JsonOptions);
		updated!.Name.ShouldBe("Blackhole Renamed");
		updated.Enable.ShouldBeFalse();

		(await client.GetAsync($"/api/v1/download-clients/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.DeleteAsync($"/api/v1/download-clients/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await client.GetAsync($"/api/v1/download-clients/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task DownloadClient_TestUnsaved_ShouldReportInvalid_WhenSettingsMissingRequiredField()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var test = await client.PostAsJsonAsync("/api/v1/download-clients/test", new
		{
			type = "QBITTORRENT",
			settings = JsonSerializer.SerializeToElement(new { })
		});

		test.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await test.Content.ReadFromJsonAsync<DownloadClientTestResult>();
		result!.IsValid.ShouldBeFalse();
		result.FieldErrors.ShouldContainKey("host");
	}

	[Fact]
	public async Task Queue_ShouldListItem_AfterDownloadMonitorReconcilesSubstitutedClient()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var clientEntity = await _factory.WithDbAsync(async db =>
		{
			var entity = new DownloadClient { Name = "Fake Client", Type = DownloadClientType.QBITTORRENT, Enable = true, SettingsJson = "{}" };
			db.DownloadClients.Add(entity);
			await db.SaveChangesAsync();
			return entity;
		});

		var fakeClient = new FakeDownloadClient(Protocol.BITTORRENT, [
			new DownloadClientItem { DownloadId = "queue-item-1", Title = "Some.Release.Title", Status = DownloadItemStatus.DOWNLOADING, TotalSize = 1000, RemainingSize = 500 }
		]);
		_factory.ClientProvider.Set(new EnabledDownloadClient(clientEntity, fakeClient));

		var enqueue = await client.PostAsJsonAsync("/api/v1/commands", new { name = "DownloadMonitor" });
		enqueue.StatusCode.ShouldBe(HttpStatusCode.Created);
		var command = await enqueue.Content.ReadFromJsonAsync<Command>(JsonOptions);

		await WaitForCommandAsync(client, command!.Id);

		var queue = await client.GetAsync("/api/v1/queue");
		queue.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await queue.Content.ReadFromJsonAsync<JsonElement>();
		var items = page.GetProperty("items").EnumerateArray().ToList();
		items.ShouldContain(x => x.GetProperty("title").GetString() == "Some.Release.Title");
	}

	[Fact]
	public async Task History_And_Blocklist_ShouldReturnSeededRows()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		await _factory.WithDbAsync(async db =>
		{
			db.HistoryEvents.Add(new HistoryEvent { Type = HistoryEventType.IMPORTED, SourceTitle = "History.Seed.Title", Date = DateTime.UtcNow });
			db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "Blocklist.Seed.Title", Protocol = Protocol.BITTORRENT, Reason = "test", Date = DateTime.UtcNow });
			return await db.SaveChangesAsync();
		});

		var history = await client.GetAsync("/api/v1/history");
		history.StatusCode.ShouldBe(HttpStatusCode.OK);
		var historyPage = await history.Content.ReadFromJsonAsync<JsonElement>();
		historyPage.GetProperty("items").EnumerateArray().ShouldContain(x => x.GetProperty("sourceTitle").GetString() == "History.Seed.Title");

		var blocklist = await client.GetAsync("/api/v1/blocklist");
		blocklist.StatusCode.ShouldBe(HttpStatusCode.OK);
		var blocklistPage = await blocklist.Content.ReadFromJsonAsync<JsonElement>();
		blocklistPage.GetProperty("items").EnumerateArray().ShouldContain(x => x.GetProperty("releaseTitle").GetString() == "Blocklist.Seed.Title");
	}

	[Fact]
	public async Task EpisodeFile_Delete_ShouldMoveFileToRecycleBin()
	{
		var client = await _factory.CreateAuthorizedClientAsync();
		var libraryRoot = Directory.CreateTempSubdirectory("submarine-it-library-").FullName;
		var recycleBin = Directory.CreateTempSubdirectory("submarine-it-recycle-").FullName;
		var versionFolder = Path.Combine(libraryRoot, "Show");
		Directory.CreateDirectory(versionFolder);
		var filePath = Path.Combine(versionFolder, "episode.mkv");
		await File.WriteAllTextAsync(filePath, "content");

		var episodeFileId = await _factory.WithDbAsync(async db =>
		{
			var mgmt = await db.MediaManagementConfig.SingleAsync();
			mgmt.RecycleBinPath = recycleBin;

			var root = new RootFolder { Path = libraryRoot, MediaKind = MediaKind.SERIES };
			db.RootFolders.Add(root);
			var series = new Series { Title = "Show", CleanTitle = "show" };
			db.Series.Add(series);
			await db.SaveChangesAsync();
			var version = new MediaVersion { SeriesId = series.Id, Name = "Default", QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = root.Id, Path = "Show" };
			db.MediaVersions.Add(version);
			await db.SaveChangesAsync();
			var file = new EpisodeFile { SeriesId = series.Id, MediaVersionId = version.Id, RelativePath = "episode.mkv", Size = 7, DateAdded = DateTime.UtcNow };
			db.EpisodeFiles.Add(file);
			await db.SaveChangesAsync();
			return file.Id;
		});

		var delete = await client.DeleteAsync($"/api/v1/episode-files/{episodeFileId}");
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		File.Exists(filePath).ShouldBeFalse();
		File.Exists(Path.Combine(recycleBin, "Show", "episode.mkv")).ShouldBeTrue();

		var afterDelete = await client.GetAsync($"/api/v1/episode-files/{episodeFileId}");
		afterDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private static async Task WaitForCommandAsync(HttpClient client, int commandId)
	{
		for (var i = 0; i < 100; i++)
		{
			var response = await client.GetAsync($"/api/v1/commands/{commandId}");
			var command = await response.Content.ReadFromJsonAsync<Command>(JsonOptions);
			if (command!.Status is CommandStatus.COMPLETED or CommandStatus.FAILED)
			{
				command.Status.ShouldBe(CommandStatus.COMPLETED, command.Exception);
				return;
			}

			await Task.Delay(100);
		}

		throw new TimeoutException($"Command {commandId} did not complete in time");
	}

	private static StringContent Json(string payload) => new(payload, Encoding.UTF8, "application/json");
}

/// <summary>Minimal fake download client reporting a fixed set of items.</summary>
public sealed class FakeDownloadClient(Protocol protocol, IReadOnlyList<DownloadClientItem> items) : IDownloadClient
{
	public DownloadClientType Type => DownloadClientType.QBITTORRENT;

	public Protocol Protocol => protocol;

	public Task<string> AddAsync(RemoteRelease release, SeedCriteria? seedCriteria, CancellationToken cancellationToken)
		=> throw new NotSupportedException();

	public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken)
		=> Task.FromResult(items);

	public Task<DownloadClientStatus> GetStatusAsync(CancellationToken cancellationToken)
		=> Task.FromResult(new DownloadClientStatus([]));

	public Task RemoveAsync(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> Task.CompletedTask;

	public Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken)
		=> Task.CompletedTask;

	public Task TestAsync(CancellationToken cancellationToken)
		=> Task.CompletedTask;
}
