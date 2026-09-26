using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.IntegrationTests.Library;

public sealed class TagDetailRelationsApiTests
{
	[Fact]
	public async Task TagDetail_ShouldListEveryTaggableRelation()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await factory.WithDbAsync(async db =>
		{
			var tag = new Tag { Label = "all-relations" };
			var series = new Series { TvdbId = 920001, Title = "Tagged series", Tags = [tag] };
			var movie = new Movie { TmdbId = 920002, Title = "Tagged movie", Tags = [tag] };
			var indexer = new Indexer { Name = "Tagged indexer", BaseUrl = "http://tagged.invalid", Tags = [tag] };
			var notification = new Notification { Name = "Tagged notification", Tags = [tag] };
			var delay = new DelayProfile { Name = "Tagged delay", Tags = [tag] };
			var release = new ReleaseProfile { Name = "Tagged release", Tags = [tag] };
			var importList = new ImportList { Name = "Tagged list", Type = ImportListType.CUSTOM, Tags = [tag] };
			db.AddRange(tag, series, movie, indexer, notification, delay, release, importList);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (
				TagId: tag.Id,
				SeriesId: series.Id,
				MovieId: movie.Id,
				IndexerId: indexer.Id,
				NotificationId: notification.Id,
				DelayProfileId: delay.Id,
				ReleaseProfileId: release.Id,
				ImportListId: importList.Id);
		});

		var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tags/{ids.TagId}/detail");
		AssertContainsId(detail.GetProperty("seriesIds"), ids.SeriesId);
		AssertContainsId(detail.GetProperty("movieIds"), ids.MovieId);
		AssertContainsId(detail.GetProperty("indexerIds"), ids.IndexerId);
		AssertContainsId(detail.GetProperty("notificationIds"), ids.NotificationId);
		AssertContainsId(detail.GetProperty("delayProfileIds"), ids.DelayProfileId);
		AssertContainsId(detail.GetProperty("releaseProfileIds"), ids.ReleaseProfileId);
		AssertContainsId(detail.GetProperty("importListIds"), ids.ImportListId);
	}

	private static void AssertContainsId(JsonElement array, int id)
		=> array.EnumerateArray().Select(item => item.GetInt32()).ShouldContain(id);
}
