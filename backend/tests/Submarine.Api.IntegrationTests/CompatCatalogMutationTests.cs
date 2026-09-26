using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatCatalogMutationTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatCatalogMutationTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task RootFolderMutations_ShouldFilterByFacadeCreateCorrectKindAndPreserveInUsePolicy()
	{
		var parent = Path.Combine(Path.GetTempPath(), $"compat-roots-{Guid.NewGuid():N}");
		var seriesPath = Directory.CreateDirectory(Path.Combine(parent, "series")).FullName;
		var moviePath = Directory.CreateDirectory(Path.Combine(parent, "movies")).FullName;
		var inUseSeriesPath = Directory.CreateDirectory(Path.Combine(parent, "in-use-series")).FullName;
		var inUseMoviePath = Directory.CreateDirectory(Path.Combine(parent, "in-use-movies")).FullName;
		try
		{
			var inUseIds = await _factory.WithDbAsync(async db =>
			{
				var seriesRoot = new RootFolder { Path = inUseSeriesPath, MediaKind = MediaKind.SERIES };
				var movieRoot = new RootFolder { Path = inUseMoviePath, MediaKind = MediaKind.MOVIES };
				db.RootFolders.AddRange(seriesRoot, movieRoot);
				await db.SaveChangesAsync();
				db.Collections.Add(new Collection { TmdbCollectionId = 1234567, Title = "Root in use", RootFolderId = movieRoot.Id });
				db.ImportLists.Add(new ImportList { Name = "Series root in use", Type = ImportListType.TMDB_LIST, MediaKind = MediaKind.SERIES, RootFolderId = seriesRoot.Id });
				await db.SaveChangesAsync();
				return (SeriesId: seriesRoot.Id, MovieId: movieRoot.Id);
			});

			using var client = await _factory.CreateAuthorizedClientAsync();
			foreach (var (facade, path, inUseId) in new[]
			{
				("sonarr", seriesPath, inUseIds.SeriesId),
				("radarr", moviePath, inUseIds.MovieId)
			})
			{
				var prefix = $"/compat/{facade}/api/v3/rootfolder";
				var create = await client.PostAsJsonAsync(prefix, new { path });
				create.StatusCode.ShouldBe(HttpStatusCode.Created);
				var root = await create.Content.ReadFromJsonAsync<JsonElement>();
				var id = root.GetProperty("id").GetInt32();
				root.GetProperty("path").GetString().ShouldBe(path);
				var folders = await client.GetFromJsonAsync<JsonElement>(prefix);
				folders.GetArrayLength().ShouldBe(2);
				folders.EnumerateArray().Count(folder => folder.GetProperty("id").GetInt32() == id).ShouldBe(1);

				var wrongFacade = facade == "sonarr" ? "radarr" : "sonarr";
				(await client.GetAsync($"/compat/{wrongFacade}/api/v3/rootfolder/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
				(await client.DeleteAsync($"/compat/{facade}/api/v3/rootfolder/{inUseId}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
				var delete = await client.DeleteAsync($"{prefix}/{id}");
				delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
			}

			(await client.PostAsJsonAsync("/compat/radarr/api/v3/rootfolder", new { path = inUseSeriesPath })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		}
		finally
		{
			Directory.Delete(parent, recursive: true);
		}
	}

	[Fact]
	public async Task TagMutations_ShouldUseLocalIdsAndRejectDeletionAcrossFacadeReferences()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		var created = await client.PostAsJsonAsync("/compat/sonarr/api/v3/tag", new { label = "Shared compatibility tag" });
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var tag = await created.Content.ReadFromJsonAsync<JsonElement>();
		var id = tag.GetProperty("id").GetInt32();
		var seed = await _factory.WithDbAsync(async db =>
		{
			var series = new Series { TvdbId = 987654301, Title = "Mutation series", Tags = [await db.Tags.SingleAsync(x => x.Id == id)] };
			var movie = new Movie { TmdbId = 987654301, Title = "Mutation movie", Tags = [await db.Tags.SingleAsync(x => x.Id == id)] };
			db.Series.Add(series);
			db.Movies.Add(movie);
			await db.SaveChangesAsync();
			return (SeriesId: series.Id, MovieId: movie.Id);
		});

		var mismatch = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/tag/{id}", new { id = id + 500, label = "Should not update" });
		mismatch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await client.GetFromJsonAsync<JsonElement>($"/compat/sonarr/api/v3/tag/{id}")).GetProperty("label").GetString().ShouldBe("Shared compatibility tag");

		var update = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/tag/{id}", new { id, label = "Updated shared tag" });
		update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var collection = await client.PutAsJsonAsync("/compat/sonarr/api/v3/tag", new[] { new { id, label = "Collection update" } });
		collection.StatusCode.ShouldBe(HttpStatusCode.OK);
		var crossFacadeDelete = await client.DeleteAsync($"/compat/sonarr/api/v3/tag/{id}");
		crossFacadeDelete.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		var relations = await _factory.WithDbAsync(async db => new
		{
			Series = await db.Series.Where(x => x.Id == seed.SeriesId).SelectMany(x => x.Tags).Select(x => x.Label).SingleAsync(),
			Movie = await db.Movies.Where(x => x.Id == seed.MovieId).SelectMany(x => x.Tags).Select(x => x.Label).SingleAsync()
		});
		relations.Series.ShouldBe("Collection update");
		relations.Movie.ShouldBe("Collection update");
	}
}
