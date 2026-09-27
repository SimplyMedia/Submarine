using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Submarine.Api.IntegrationTests.Downloads;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Downloads;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatQueueRouteTests : IClassFixture<DownloadsApiFactory>
{
	private readonly DownloadsApiFactory _factory;

	public CompatQueueRouteTests(DownloadsApiFactory factory) => _factory = factory;

	[Fact]
	public async Task QueueRoutes_PageByFacadeAndRemoveOnlyWhenEveryBulkIdIsValid()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		var data = await SeedQueueAsync();
		var downloadClient = new QueueRemovalClient();
		_factory.ClientProvider.Set(new EnabledDownloadClient(data.DownloadClient, downloadClient));

		var firstPage = await GetJsonAsync(client, "/compat/sonarr/api/v3/queue?page=1&pageSize=1&sortKey=id");
		firstPage.GetProperty("totalRecords").GetInt32().ShouldBe(2);
		firstPage.GetProperty("page").GetInt32().ShouldBe(1);
		firstPage.GetProperty("pageSize").GetInt32().ShouldBe(1);
		firstPage.GetProperty("records").GetArrayLength().ShouldBe(1);
		var secondPage = await GetJsonAsync(client, "/compat/sonarr/api/v3/queue?page=2&pageSize=1&sortKey=id");
		secondPage.GetProperty("records").GetArrayLength().ShouldBe(1);
		secondPage.GetProperty("records")[0].GetProperty("id").GetInt32().ShouldBe(data.SecondSelectedId);
		var queueItem = firstPage.GetProperty("records")[0];
		queueItem.GetProperty("status").GetString().ShouldBe("downloading");
		queueItem.GetProperty("size").GetInt64().ShouldBe(1000);
		queueItem.GetProperty("sizeleft").GetInt64().ShouldBe(250);
		queueItem.GetProperty("outputPath").GetString().ShouldBe("/downloads/selected");
		queueItem.GetProperty("timeleft").ValueKind.ShouldBe(JsonValueKind.Null);
		var status = await GetJsonAsync(client, "/compat/sonarr/api/v3/queue/status");
		status.GetProperty("totalCount").GetInt32().ShouldBe(2);
		var movieQueue = await GetJsonAsync(client, "/compat/radarr/api/v3/queue?page=1&pageSize=10");
		movieQueue.GetProperty("totalRecords").GetInt32().ShouldBe(1);
		movieQueue.GetProperty("records")[0].GetProperty("id").GetInt32().ShouldBe(data.MovieId);
		var details = await GetJsonAsync(client, "/compat/sonarr/api/v3/queue/details");
		details.GetArrayLength().ShouldBe(2);

		using var invalidBulkRequest = new HttpRequestMessage(HttpMethod.Delete, "/compat/sonarr/api/v3/queue/bulk")
		{
			Content = JsonContent.Create(new
			{
				ids = new[] { data.FirstSelectedId, int.MaxValue },
				removeFromClient = true,
				blocklist = true,
				skipRedownload = true
			})
		};
		var invalidBulk = await client.SendAsync(invalidBulkRequest);
		invalidBulk.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await _factory.WithDbAsync(db => db.TrackedDownloads.AnyAsync(x => x.Id == data.FirstSelectedId))).ShouldBeTrue();
		(await _factory.WithDbAsync(db => db.BlocklistItems.AnyAsync(x => x.ReleaseTitle == "Selected Release One"))).ShouldBeFalse();
		downloadClient.RemovedIds.ShouldBeEmpty();

		var delete = await client.DeleteAsync($"/compat/sonarr/api/v3/queue/{data.FirstSelectedId}?removeFromClient=true&blocklist=true&skipRedownload=true");
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		downloadClient.RemovedIds.ShouldContain("download-selected-one");
		(await _factory.WithDbAsync(db => db.TrackedDownloads.AnyAsync(x => x.Id == data.FirstSelectedId))).ShouldBeFalse();
		(await _factory.WithDbAsync(db => db.BlocklistItems.AnyAsync(x => x.ReleaseTitle == "Selected Release One"))).ShouldBeTrue();
		(await _factory.WithDbAsync(db => db.Commands.AnyAsync(x => x.Name.Contains("Search")))).ShouldBeFalse();
		using var validBulkRequest = new HttpRequestMessage(HttpMethod.Delete, "/compat/sonarr/api/v3/queue/bulk")
		{
			Content = JsonContent.Create(new
			{
				ids = new[] { data.SecondSelectedId },
				removeFromClient = true,
				blocklist = true,
				skipRedownload = true
			})
		};
		var validBulk = await client.SendAsync(validBulkRequest);
		validBulk.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await validBulk.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("removed").GetInt32().ShouldBe(1);
		downloadClient.RemovedIds.ShouldContain("download-selected-two");
		(await _factory.WithDbAsync(db => db.BlocklistItems.AnyAsync(x => x.ReleaseTitle == "Selected Release Two"))).ShouldBeTrue();
	}

	private async Task<QueueIds> SeedQueueAsync()
	{
		return await _factory.WithDbAsync(async db =>
		{
			var downloadClient = new DownloadClient { Name = "Compat Queue Client", Type = DownloadClientType.QBITTORRENT, Enable = true, SettingsJson = "{}" };
			var series = new Series { TvdbId = 812345671, Title = "Compat Queue Series", CleanTitle = "compatqueueseries" };
			var movie = new Movie { TmdbId = 812345671, Title = "Compat Queue Movie" };
			var root = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "compat-queue-" + Guid.NewGuid().ToString("N")), MediaKind = MediaKind.SERIES };
			db.DownloadClients.Add(downloadClient);
			db.Series.Add(series);
			db.Movies.Add(movie);
			db.RootFolders.Add(root);
			await db.SaveChangesAsync();

			var selected = new MediaVersion { Name = "Selected", SeriesId = series.Id, QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = root.Id, Path = "selected" };
			var sibling = new MediaVersion { Name = "Sibling", SeriesId = series.Id, QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = root.Id, Path = "sibling" };
			var movieRoot = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "compat-queue-movie-" + Guid.NewGuid().ToString("N")), MediaKind = MediaKind.MOVIES };
			db.RootFolders.Add(movieRoot);
			await db.SaveChangesAsync();
			var movieVersion = new MediaVersion { Name = "Movie", MovieId = movie.Id, QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = movieRoot.Id, Path = "movie" };
			db.MediaVersions.AddRange(selected, sibling, movieVersion);
			await db.SaveChangesAsync();

			var first = NewDownload(downloadClient, series, selected, "Selected Release One", "download-selected-one", "/downloads/selected", TrackedDownloadStatus.DOWNLOADING, 1000, 250);
			first.EpisodeIds = [123456789];
			var second = NewDownload(downloadClient, series, selected, "Selected Release Two", "download-selected-two", null, TrackedDownloadStatus.QUEUED, 500, 500);
			var siblingDownload = NewDownload(downloadClient, series, sibling, "Sibling Release", "download-sibling", null, TrackedDownloadStatus.DOWNLOADING, 200, 100);
			var movieDownload = NewDownload(downloadClient, null, movieVersion, "Movie Release", "download-movie", null, TrackedDownloadStatus.COMPLETED, 900, 0);
			movieDownload.MovieId = movie.Id;
			db.TrackedDownloads.AddRange(first, second, siblingDownload, movieDownload);
			await db.SaveChangesAsync();
			return new QueueIds(downloadClient, first.Id, second.Id, movieDownload.Id);
		});
	}

	private static TrackedDownload NewDownload(DownloadClient client, Series? series, MediaVersion version, string title, string downloadId, string? outputPath, TrackedDownloadStatus status, long size, long sizeLeft)
		=> new()
		{
			DownloadClient = client,
			Title = title,
			ReleaseTitle = title,
			DownloadId = downloadId,
			Protocol = Protocol.BITTORRENT,
			Status = status,
			State = TrackedDownloadState.DOWNLOADING,
			SeriesId = series?.Id,
			MovieId = version.MovieId,
			MediaVersionId = version.Id,
			EpisodeIds = [],
			Size = size,
			SizeLeft = sizeLeft,
			OutputPath = outputPath,
			Added = DateTime.UtcNow
		};

	private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
	{
		var response = await client.GetAsync(path);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
	}

	private sealed record QueueIds(DownloadClient DownloadClient, int FirstSelectedId, int SecondSelectedId, int MovieId);

	private sealed class QueueRemovalClient : IDownloadClient
	{
		public List<string> RemovedIds { get; } = [];
		public Protocol Protocol => Submarine.Core.Provider.Protocol.BITTORRENT;
		public DownloadClientType Type => DownloadClientType.QBITTORRENT;
		public Task<string> AddAsync(RemoteRelease release, SeedCriteria? seedCriteria, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DownloadClientItem>>([]);
		public Task<DownloadClientStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new DownloadClientStatus([]));
		public Task RemoveAsync(string downloadId, bool deleteData, CancellationToken cancellationToken)
		{
			RemovedIds.Add(downloadId);
			return Task.CompletedTask;
		}
		public Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken) => Task.CompletedTask;
		public Task TestAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
