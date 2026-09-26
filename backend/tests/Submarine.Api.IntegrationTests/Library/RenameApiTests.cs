using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Xunit;

namespace Submarine.Api.IntegrationTests.Library;

public sealed class RenameApiTests
{
	[Fact]
	public async Task RenameApi_ShouldPreviewChangedMoviePathAndQueueSelectedFileRename()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = Directory.CreateTempSubdirectory("submarine-rename-api-").FullName;
		try
		{
			var ids = await factory.WithDbAsync(async db =>
			{
				var qualityProfileId = await db.QualityProfiles.OrderBy(profile => profile.Id).Select(profile => profile.Id).FirstAsync(TestContext.Current.CancellationToken);
				var languageProfileId = await db.LanguageProfiles.OrderBy(profile => profile.Id).Select(profile => profile.Id).FirstAsync(TestContext.Current.CancellationToken);
				var folder = new RootFolder { Path = root, MediaKind = Submarine.Core.Enums.MediaKind.MOVIES };
				var movie = new Movie { TmdbId = 922001, Title = "Rename Movie", Year = 2020 };
				db.AddRange(folder, movie);
				await db.SaveChangesAsync(TestContext.Current.CancellationToken);
				var version = new MediaVersion { Name = "Main", MovieId = movie.Id, QualityProfileId = qualityProfileId, LanguageProfileId = languageProfileId, RootFolderId = folder.Id, Path = "Rename Movie" };
				db.MediaVersions.Add(version);
				await db.SaveChangesAsync(TestContext.Current.CancellationToken);
				var file = new MovieFile { MovieId = movie.Id, MediaVersionId = version.Id, RelativePath = "old-file.mkv", DateAdded = DateTime.UtcNow, Size = 1000 };
				db.MovieFiles.Add(file);
				await db.SaveChangesAsync(TestContext.Current.CancellationToken);
				return (MovieId: movie.Id, FileId: file.Id);
			});

			var preview = await client.GetFromJsonAsync<JsonElement>($"/api/v1/rename?movieId={ids.MovieId}");
			preview.GetArrayLength().ShouldBe(1);
			preview[0].GetProperty("fileId").GetInt32().ShouldBe(ids.FileId);
			preview[0].GetProperty("existingPath").GetString().ShouldBe("old-file.mkv");
			preview[0].GetProperty("newPath").GetString().ShouldBe("Rename Movie (2020).mkv");

			var execute = await client.PostAsJsonAsync("/api/v1/rename", new { movieId = ids.MovieId, fileIds = new[] { ids.FileId } });
			execute.StatusCode.ShouldBe(HttpStatusCode.OK);
			var command = await execute.Content.ReadFromJsonAsync<JsonElement>();
			command.GetProperty("name").GetString().ShouldBe("RenameMovie");
			var body = JsonDocument.Parse(command.GetProperty("body").GetString()!);
			body.RootElement.GetProperty("movieId").GetInt32().ShouldBe(ids.MovieId);
			body.RootElement.GetProperty("fileIds")[0].GetInt32().ShouldBe(ids.FileId);

			(await client.GetAsync("/api/v1/rename?movieId=2147483000")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
			var invalid = await client.PostAsJsonAsync("/api/v1/rename", new { fileIds = new[] { ids.FileId } });
			invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
