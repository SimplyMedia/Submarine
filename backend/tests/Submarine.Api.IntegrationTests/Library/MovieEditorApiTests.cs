using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.IntegrationTests.Library;

public sealed class MovieEditorApiTests
{
	[Fact]
	public async Task MovieEditor_ShouldBulkUpdateMonitoringAvailabilityAndTagsOnlyForSelectedMovies()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await factory.WithDbAsync(async db =>
		{
			var tag = new Tag { Label = "editor-tag" };
			var selected = new Movie { TmdbId = 925001, Title = "Selected movie" };
			var unselected = new Movie { TmdbId = 925002, Title = "Unselected movie" };
			db.AddRange(tag, selected, unselected);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (TagId: tag.Id, SelectedId: selected.Id, UnselectedId: unselected.Id);
		});

		var update = await client.PutAsJsonAsync("/api/v1/movies/editor", new
		{
			ids = new[] { ids.SelectedId },
			monitored = false,
			minimumAvailability = "RELEASED",
			tags = new { mode = "add", tagIds = new[] { ids.TagId } }
		});
		update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await update.Content.ReadFromJsonAsync<JsonElement>();
		result.GetProperty("updated").GetInt32().ShouldBe(1);

		var movies = await factory.WithDbAsync(async db => await db.Movies.Include(movie => movie.Tags).ToDictionaryAsync(movie => movie.Id, TestContext.Current.CancellationToken));
		movies[ids.SelectedId].Monitored.ShouldBeFalse();
		movies[ids.SelectedId].MinimumAvailability.ShouldBe(MinimumAvailability.RELEASED);
		movies[ids.SelectedId].Tags.Select(tag => tag.Id).ShouldContain(ids.TagId);
		movies[ids.UnselectedId].Monitored.ShouldBeTrue();
		movies[ids.UnselectedId].Tags.ShouldBeEmpty();
	}
}
