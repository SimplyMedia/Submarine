using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Api.Features.LibraryImport;
using Submarine.Api.Features.ManualImport;
using Submarine.Contracts.Metadata;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Api.IntegrationTests.Downloads;

public sealed class ManualAndLibraryImportApiTests : IClassFixture<DownloadsApiFactory>
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
	};

	private readonly DownloadsApiFactory _factory;

	public ManualAndLibraryImportApiTests(DownloadsApiFactory factory) => _factory = factory;


	[Fact]
	public async Task ManualImport_ShouldImportFakeFile_EndToEnd()
	{
		var client = await _factory.CreateAuthorizedClientAsync();
		var libraryRoot = Directory.CreateTempSubdirectory("submarine-it-manual-library-").FullName;
		var downloadFolder = Directory.CreateTempSubdirectory("submarine-it-manual-download-").FullName;
		var sourceFile = Path.Combine(downloadFolder, "Manual.Show.S01E01.1080p.BluRay.x264-GROUP.mkv");
		await File.WriteAllTextAsync(sourceFile, "video");

		var (seriesId, episodeId, versionId) = await _factory.WithDbAsync(async db =>
		{
			var mgmt = await db.MediaManagementConfig.SingleAsync();
			mgmt.UseHardlinks = false;
			var root = new RootFolder { Path = libraryRoot, MediaKind = MediaKind.SERIES };
			db.RootFolders.Add(root);
			var quality = new QualityProfile
			{
				Name = "Manual Import Quality",
				UpgradeAllowed = true,
				Cutoff = 0,
				Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R1080_P), true)]
			};
			db.QualityProfiles.Add(quality);
			var language = new LanguageProfile { Name = "Manual Import Language", Languages = [Submarine.Core.Languages.Language.ENGLISH], Cutoff = Submarine.Core.Languages.Language.ENGLISH };
			db.LanguageProfiles.Add(language);
			var series = new Series { Title = "Manual Show", CleanTitle = "manualshow" };
			series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" });
			db.Series.Add(series);
			await db.SaveChangesAsync();
			var version = new MediaVersion { SeriesId = series.Id, Name = "Default", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "Manual Show" };
			db.MediaVersions.Add(version);
			await db.SaveChangesAsync();
			return (series.Id, series.Episodes.First().Id, version.Id);
		});

		var analyze = await client.GetAsync($"/api/v1/manual-import?folder={Uri.EscapeDataString(downloadFolder)}&seriesId={seriesId}");
		analyze.StatusCode.ShouldBe(HttpStatusCode.OK);
		var candidates = await analyze.Content.ReadFromJsonAsync<List<ManualImportCandidateDto>>(JsonOptions);
		candidates!.ShouldContain(x => x.Path == sourceFile);

		var import = await client.PostAsJsonAsync("/api/v1/manual-import", new
		{
			items = new[]
			{
				new
				{
					path = sourceFile,
					seriesId,
					episodeIds = new[] { episodeId },
					movieId = (int?)null,
					mediaVersionId = versionId,
					quality = (object?)null,
					languages = (object?)null,
					releaseGroup = (string?)null,
					downloadId = (string?)null
				}
			},
			importMode = "move"
		});
		import.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await import.Content.ReadFromJsonAsync<ManualImportResultDto>();
		result!.AnyImported.ShouldBeTrue(JsonSerializer.Serialize(result));

		File.Exists(sourceFile).ShouldBeFalse();
		var fileCount = await _factory.WithDbAsync(db => db.EpisodeFiles.CountAsync());
		fileCount.ShouldBe(1);
	}

	[Fact]
	public async Task LibraryImport_Scan_ShouldProposeMetadataMatches_ForUnmappedFolder()
	{
		var client = await _factory.CreateAuthorizedClientAsync();
		var libraryRoot = Directory.CreateTempSubdirectory("submarine-it-libimport-").FullName;
		Directory.CreateDirectory(Path.Combine(libraryRoot, "Existing Show (2020)"));

		_factory.Metadata.Series[9001] = new SeriesResource(
			TvdbId: 9001,
			TmdbId: null,
			ImdbId: null,
			Title: "Existing Show",
			SortTitle: null,
			Overview: "Overview",
			FirstAired: new DateOnly(2020, 1, 1),
			Status: Submarine.Contracts.Metadata.SeriesStatus.CONTINUING,
			Runtime: 30,
			Network: "Network",
			Genres: [],
			Certification: null,
			Seasons: [],
			Episodes: [],
			PosterUrl: null,
			BackdropUrl: null,
			Year: 2020,
			AlternateTitles: []);

		var rootFolderId = await _factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = libraryRoot, MediaKind = MediaKind.SERIES };
			db.RootFolders.Add(root);
			await db.SaveChangesAsync();
			return root.Id;
		});

		var scan = await client.PostAsJsonAsync("/api/v1/library-import/scan", new { rootFolderId });
		scan.StatusCode.ShouldBe(HttpStatusCode.OK);
		var folders = await scan.Content.ReadFromJsonAsync<List<LibraryImportFolderDto>>();

		folders!.ShouldContain(x => x.Folder == "Existing Show (2020)" && x.GuessedYear == 2020);
	}
}
