using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Api.IntegrationTests.Library;
using Submarine.Contracts.Metadata;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatRadarrTests
{
	[Fact]
	public async Task MovieListAndLookupHydrateFacadeIdentityWithSelectedVersion()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await SeedMovieAsync(factory);
		var fixture = MovieResource(ids.TmdbId, "Facade title", "tt1234567");
		factory.Metadata.Movies[ids.TmdbId] = fixture;

		using var denied = factory.CreateClient();
		(await denied.GetAsync("/compat/radarr/api/v3/movie")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

		var listResponse = await client.GetAsync("/compat/radarr/api/v3/movie");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var movies = await listResponse.Content.ReadFromJsonAsync<JsonElement[]>();
		movies.ShouldNotBeNull();
		movies!.Length.ShouldBe(1);
		movies[0].GetProperty("id").GetInt32().ShouldBe(ids.MovieId);
		movies[0].GetProperty("tmdbId").GetInt32().ShouldBe(ids.TmdbId);
		movies[0].GetProperty("qualityProfileId").GetInt32().ShouldBe(ids.QualityProfileId);
		movies[0].GetProperty("path").GetString().ShouldBe(Path.Combine(ids.RootPath, "Feature title"));

		foreach (var term in new[] { $"tmdb:{ids.TmdbId}", "imdb:tt1234567" })
		{
			var lookup = await client.GetFromJsonAsync<JsonElement[]>("/compat/radarr/api/v3/movie/lookup?term=" + Uri.EscapeDataString(term));
			lookup.ShouldNotBeNull();
			lookup!.Length.ShouldBe(1);
			lookup[0].GetProperty("id").GetInt32().ShouldBe(ids.MovieId);
			lookup[0].GetProperty("tmdbId").GetInt32().ShouldBe(ids.TmdbId);
		}
	}

	[Fact]
	public async Task RadarrMovieAndCreditProjectionUsesTmdbCredits()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await SeedMovieAsync(factory);
		factory.Metadata.Movies[ids.TmdbId] = MovieResource(ids.TmdbId, "Facade title", "tt1234567") with
		{
			Credits = [new MovieCreditResource(1234, "Cast member", "/person.jpg", "Acting", null, "Lead", 0)]
		};
		var credits = await client.GetFromJsonAsync<JsonElement[]>($"/compat/radarr/api/v3/credits?movieId={ids.MovieId}");
		credits.ShouldNotBeNull();
		credits!.Length.ShouldBe(1);
		credits[0].GetProperty("movieId").GetInt32().ShouldBe(ids.MovieId);
		credits[0].GetProperty("personTmdbId").GetInt32().ShouldBe(1234);
		credits[0].GetProperty("personName").GetString().ShouldBe("Cast member");
	}

	[Fact]
	public async Task DeletingBoundMovieVersionKeepsSiblingAndTombstonesRadarrIdentity()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await SeedMovieAsync(factory);
		var siblingId = await factory.WithDbAsync(async db =>
		{
			var sibling = new MediaVersion
			{
				Name = "sibling", MovieId = ids.MovieId, QualityProfileId = ids.QualityProfileId,
				LanguageProfileId = await db.LanguageProfiles.Select(x => x.Id).FirstAsync(TestContext.Current.CancellationToken),
				RootFolderId = await db.RootFolders.Where(x => x.Path == ids.RootPath).Select(x => x.Id).FirstAsync(TestContext.Current.CancellationToken),
				Path = "Sibling"
			};
			db.MediaVersions.Add(sibling);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return sibling.Id;
		});
		var response = await client.DeleteAsync($"/compat/radarr/api/v3/movie/{ids.MovieId}?deleteFiles=false&addImportExclusion=true");
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var state = await factory.WithDbAsync(async db => new
		{
			MovieExists = await db.Movies.AnyAsync(x => x.Id == ids.MovieId),
			RemainingVersions = await db.MediaVersions.Where(x => x.MovieId == ids.MovieId).Select(x => x.Id).ToListAsync(),
			Binding = await db.CompatLibraryBindings.SingleAsync(x => x.Facade == "radarr" && x.MovieId == ids.MovieId),
			ExclusionExists = await db.ImportListExclusions.AnyAsync(x => x.TmdbId == ids.TmdbId)
		});
		state.MovieExists.ShouldBeTrue();
		state.RemainingVersions.ShouldBe([siblingId]);
		state.Binding.Excluded.ShouldBeTrue();
		state.Binding.MediaVersionId.ShouldBeNull();
		state.ExclusionExists.ShouldBeTrue();
		var list = await client.GetFromJsonAsync<JsonElement[]>("/compat/radarr/api/v3/movie");
		list.ShouldNotBeNull();
		list!.ShouldBeEmpty();
	}

	[Fact]
	public async Task DeleteMovieWithoutOptionalFlagsUsesUpstreamDefaults()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await SeedMovieAsync(factory);

		var response = await client.DeleteAsync($"/compat/radarr/api/v3/movie/{ids.MovieId}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await factory.WithDbAsync(db => db.Movies.AnyAsync(x => x.Id == ids.MovieId))).ShouldBeFalse();
	}

	[Fact]
	public async Task ImportListMoviesWithoutRecommendationsFlagReturnsList()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();

		var response = await client.GetAsync("/compat/radarr/api/v3/importlist/movie");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await response.Content.ReadFromJsonAsync<JsonElement[]>()).ShouldNotBeNull();
	}

	[Fact]
	public async Task MovieAddCreatesNativeTitleAndBindsTheCreatedVersion()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = await SeedCreationResourcesAsync(factory);
		const int tmdbId = 947211;
		factory.Metadata.Movies[tmdbId] = MovieResource(tmdbId, "Requested title", "tt9472110");

		var response = await client.PostAsJsonAsync("/compat/radarr/api/v3/movie", new
		{
			tmdbId,
			title = "Requested title",
			qualityProfileId = root.QualityProfileId,
			rootFolderPath = root.RootPath,
			monitored = true,
			addOptions = new { searchForMovie = false }
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var resource = await response.Content.ReadFromJsonAsync<JsonElement>();
		var localId = resource.GetProperty("id").GetInt32();
		localId.ShouldBeGreaterThan(0);
		resource.GetProperty("tmdbId").GetInt32().ShouldBe(tmdbId);
		var state = await factory.WithDbAsync(async db => new
		{
			Movie = await db.Movies.SingleAsync(x => x.Id == localId),
			Version = await db.MediaVersions.SingleAsync(x => x.MovieId == localId),
			Binding = await db.CompatLibraryBindings.SingleAsync(x => x.MovieId == localId && x.Facade == "radarr")
		});
		state.Movie.TmdbId.ShouldBe(tmdbId);
		state.Binding.MediaVersionId.ShouldBe(state.Version.Id);
		state.Binding.Excluded.ShouldBeFalse();
	}

	private static async Task<(int MovieId, int TmdbId, int QualityProfileId, string RootPath)> SeedMovieAsync(LibraryApiFactory factory)
	{
		var rootPath = Path.Combine(Path.GetTempPath(), "radarr-compat-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(rootPath);
		return await factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = rootPath, MediaKind = MediaKind.MOVIES };
			var quality = new QualityProfile { Name = "Radarr compat quality" };
			var language = new LanguageProfile { Name = "Radarr compat language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			var movie = new Movie { TmdbId = 947210, ImdbId = "tt1234567", Title = "Facade title", SortTitle = "facade title", CleanTitle = "facadetitle", Year = 2024 };
			db.AddRange(root, quality, language, movie);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var version = new MediaVersion { Name = "main", MovieId = movie.Id, QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "Feature title" };
			db.MediaVersions.Add(version);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (movie.Id, movie.TmdbId, quality.Id, rootPath);
		});
	}

	private static async Task<(int QualityProfileId, string RootPath)> SeedCreationResourcesAsync(LibraryApiFactory factory)
	{
		var rootPath = Path.Combine(Path.GetTempPath(), "radarr-compat-root-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(rootPath);
		return await factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = rootPath, MediaKind = MediaKind.MOVIES };
			var quality = new QualityProfile { Name = "Radarr add quality" };
			var language = new LanguageProfile { Name = "Radarr add language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			db.AddRange(root, quality, language);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (quality.Id, rootPath);
		});
	}

	private static MovieResource MovieResource(int tmdbId, string title, string imdbId)
		=> new(tmdbId, imdbId, title, title, title, "A test movie", new DateOnly(2024, 1, 1), null, null,
			Submarine.Contracts.Metadata.MovieStatus.RELEASED, 2024, 110, ["Drama"], "Test Studio", "PG", null, null,
			null, null, null, []);
}
