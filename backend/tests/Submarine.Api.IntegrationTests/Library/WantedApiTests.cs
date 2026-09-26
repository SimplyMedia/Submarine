using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests.Library;

public sealed class WantedApiTests
{
	[Fact]
	public async Task WantedEndpoints_ShouldPageMissingAndCutoffItems_AndQueueBothSearchCommands()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = Directory.CreateTempSubdirectory("submarine-wanted-api-").FullName;
		try
		{
			await factory.WithDbAsync(async db =>
			{
				var qualityProfile = new QualityProfile
				{
					Name = "Wanted cutoff profile",
					Cutoff = 1,
					Items =
					[
						new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P), true),
						new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)
					]
				};
				var languageProfile = new LanguageProfile { Name = "Wanted language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
				var folder = new RootFolder { Path = root, MediaKind = Submarine.Core.Enums.MediaKind.MOVIES };
				db.AddRange(qualityProfile, languageProfile, folder);
				await db.SaveChangesAsync(TestContext.Current.CancellationToken);

				for (var i = 0; i < 2; i++)
				{
					var missing = new Movie { TmdbId = 924001 + i, Title = $"Missing movie {i}", DigitalReleaseDate = DateTime.UtcNow.AddDays(-10) };
					var cutoff = new Movie { TmdbId = 924101 + i, Title = $"Cutoff movie {i}", DigitalReleaseDate = DateTime.UtcNow.AddDays(-10) };
					db.Movies.AddRange(missing, cutoff);
					await db.SaveChangesAsync(TestContext.Current.CancellationToken);
					var missingVersion = new MediaVersion { Name = "Main", MovieId = missing.Id, QualityProfileId = qualityProfile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = folder.Id, Path = missing.Title };
					db.MediaVersions.Add(missingVersion);
					var version = new MediaVersion { Name = "Main", MovieId = cutoff.Id, QualityProfileId = qualityProfile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = folder.Id, Path = cutoff.Title };
					db.MediaVersions.Add(version);
					await db.SaveChangesAsync(TestContext.Current.CancellationToken);
					db.MovieFiles.Add(new MovieFile
					{
						MovieId = cutoff.Id,
						MediaVersionId = version.Id,
						RelativePath = "cutoff.mkv",
						Size = 1000,
					DateAdded = DateTime.UtcNow,
					Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P), new Revision())
					});
				}
				await db.SaveChangesAsync(TestContext.Current.CancellationToken);
				return 0;
			});

			var missing = await client.GetFromJsonAsync<JsonElement>("/api/v1/wanted/missing?page=1&pageSize=1");
			missing.GetProperty("items").GetArrayLength().ShouldBe(1);
			missing.GetProperty("totalCount").GetInt32().ShouldBe(2);
			var cutoffItems = await client.GetFromJsonAsync<JsonElement>("/api/v1/wanted/cutoff?page=2&pageSize=1");
			cutoffItems.GetProperty("items").GetArrayLength().ShouldBe(1);
			cutoffItems.GetProperty("totalCount").GetInt32().ShouldBe(2);

			var missingSearch = await client.PostAsync("/api/v1/wanted/missing/search", null);
			missingSearch.StatusCode.ShouldBe(HttpStatusCode.Created);
			(await missingSearch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().ShouldBe("MissingSearch");
			var cutoffSearch = await client.PostAsync("/api/v1/wanted/cutoff/search", null);
			cutoffSearch.StatusCode.ShouldBe(HttpStatusCode.Created);
			(await cutoffSearch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().ShouldBe("CutoffUnmetSearch");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
