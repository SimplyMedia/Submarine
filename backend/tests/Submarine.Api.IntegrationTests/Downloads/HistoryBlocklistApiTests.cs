using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.IntegrationTests.Downloads;

public sealed class HistoryBlocklistApiTests
{
	[Fact]
	public async Task HistoryApi_ShouldPageFilterSearchAndReturnSinceAndMediaSpecificResults()
	{
		await using var factory = new SubmarineApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var now = DateTime.UtcNow;
		var ids = await factory.WithDbAsync(async db =>
		{
			var series = new Series { TvdbId = 921001, Title = "History show" };
			var movie = new Movie { TmdbId = 921002, Title = "History movie" };
			db.AddRange(series, movie);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var old = new HistoryEvent { Type = HistoryEventType.IMPORTED, SeriesId = series.Id, SourceTitle = "old-import", Date = now.AddDays(-4) };
			var recent = new HistoryEvent { Type = HistoryEventType.GRABBED, SeriesId = series.Id, SourceTitle = "wanted-release", DownloadId = "history-download", Date = now };
			var movieEvent = new HistoryEvent { Type = HistoryEventType.IMPORTED, MovieId = movie.Id, SourceTitle = "movie-import", Date = now.AddMinutes(-1) };
			db.HistoryEvents.AddRange(old, recent, movieEvent);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (SeriesId: series.Id, MovieId: movie.Id, RecentId: recent.Id);
		});

		var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/history?page=1&pageSize=1");
		page.GetProperty("items").GetArrayLength().ShouldBe(1);
		page.GetProperty("totalCount").GetInt32().ShouldBe(3);
		var search = await client.GetFromJsonAsync<JsonElement>($"/api/v1/history?seriesId={ids.SeriesId}&eventType=GRABBED&q=wanted&page=1&pageSize=10");
		search.GetProperty("totalCount").GetInt32().ShouldBe(1);
		search.GetProperty("items")[0].GetProperty("id").GetInt32().ShouldBe(ids.RecentId);
		var since = await client.GetFromJsonAsync<JsonElement>($"/api/v1/history/since?date={Uri.EscapeDataString(now.AddDays(-1).ToString("O"))}");
		since.GetArrayLength().ShouldBe(2);
		var forSeries = await client.GetFromJsonAsync<JsonElement>($"/api/v1/history/series?seriesId={ids.SeriesId}");
		forSeries.GetArrayLength().ShouldBe(2);
		var forMovie = await client.GetFromJsonAsync<JsonElement>($"/api/v1/history/movie?movieId={ids.MovieId}");
		forMovie.GetArrayLength().ShouldBe(1);
		forMovie[0].GetProperty("sourceTitle").GetString().ShouldBe("movie-import");
	}

	[Fact]
	public async Task BlocklistApi_ShouldPageDeleteSingleAndBulk_AndEnqueueDeleteAll()
	{
		await using var factory = new SubmarineApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await factory.WithDbAsync(async db =>
		{
			var items = new[]
			{
				new BlocklistItem { ReleaseTitle = "block-one", Protocol = Protocol.BITTORRENT, Reason = "first", Date = DateTime.UtcNow.AddSeconds(-2) },
				new BlocklistItem { ReleaseTitle = "block-two", Protocol = Protocol.USENET, Reason = "second", Date = DateTime.UtcNow.AddSeconds(-1) },
				new BlocklistItem { ReleaseTitle = "block-three", Protocol = Protocol.BITTORRENT, Reason = "third", Date = DateTime.UtcNow }
			};
			db.BlocklistItems.AddRange(items);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return items.Select(item => item.Id).ToArray();
		});

		var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/blocklist?page=2&pageSize=1");
		page.GetProperty("totalCount").GetInt32().ShouldBe(3);
		page.GetProperty("items").GetArrayLength().ShouldBe(1);
		(await client.DeleteAsync($"/api/v1/blocklist/{ids[0]}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var bulkRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/blocklist/bulk") { Content = JsonContent.Create(new { ids = new[] { ids[1] } }) };
		var bulk = await client.SendAsync(bulkRequest);
		bulk.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await bulk.Content.ReadFromJsonAsync<JsonElement>();
		result.GetProperty("removed").GetInt32().ShouldBe(1);
		var all = await client.DeleteAsync("/api/v1/blocklist/all");
		all.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var command = await all.Content.ReadFromJsonAsync<JsonElement>();
		command.GetProperty("name").GetString().ShouldBe("ClearBlocklist");
	}
}
