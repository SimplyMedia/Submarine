using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.IntegrationTests.Downloads;

public sealed class QueueActionsApiTests
{
	[Fact]
	public async Task QueueApi_ShouldRemoveAndBlocklistItem_AndRetryMovieWithSearchCommand()
	{
		await using var factory = new SubmarineApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await factory.WithDbAsync(async db =>
		{
			var downloadClient = new DownloadClient { Name = "Queue actions", Type = DownloadClientType.QBITTORRENT };
			var movie = new Movie { TmdbId = 923001, Title = "Queued movie" };
			db.AddRange(downloadClient, movie);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var blocked = new TrackedDownload
			{
				DownloadClientId = downloadClient.Id,
				DownloadId = "queue-remove",
				Title = "release-to-block",
				ReleaseTitle = "release-to-block",
				Protocol = Protocol.BITTORRENT,
				MovieId = movie.Id,
				State = TrackedDownloadState.DOWNLOADING,
				Added = DateTime.UtcNow,
				Size = 1234
			};
			var retry = new TrackedDownload
			{
				DownloadClientId = downloadClient.Id,
				DownloadId = "queue-retry",
				Title = "release-to-retry",
				Protocol = Protocol.BITTORRENT,
				MovieId = movie.Id,
				State = TrackedDownloadState.DOWNLOADING,
				Added = DateTime.UtcNow
			};
			db.TrackedDownloads.AddRange(blocked, retry);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (BlockedId: blocked.Id, RetryId: retry.Id, MovieId: movie.Id);
		});

		var removed = await client.DeleteAsync($"/api/v1/queue/{ids.BlockedId}?blocklist=true&skipRedownload=true");
		removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var blocklist = await client.GetFromJsonAsync<JsonElement>($"/api/v1/blocklist?page=1&pageSize=10");
		blocklist.GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("releaseTitle").GetString() == "release-to-block");
		(await factory.WithDbAsync(db => db.TrackedDownloads.AnyAsync(item => item.Id == ids.BlockedId, TestContext.Current.CancellationToken))).ShouldBeFalse();

		var retried = await client.PostAsync($"/api/v1/queue/grab/{ids.RetryId}", null);
		retried.StatusCode.ShouldBe(HttpStatusCode.OK);
		var command = await retried.Content.ReadFromJsonAsync<JsonElement>();
		command.GetProperty("name").GetString().ShouldBe("MovieSearch");
		var body = JsonDocument.Parse(command.GetProperty("body").GetString()!);
		body.RootElement.GetProperty("movieIds")[0].GetInt32().ShouldBe(ids.MovieId);
		(await factory.WithDbAsync(db => db.TrackedDownloads.AnyAsync(item => item.Id == ids.RetryId, TestContext.Current.CancellationToken))).ShouldBeFalse();
	}
}
