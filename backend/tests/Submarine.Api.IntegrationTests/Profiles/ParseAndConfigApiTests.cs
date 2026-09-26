using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests.Profiles;

public sealed class ParseAndConfigApiTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public ParseAndConfigApiTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task Parse_ShouldReturnParsedRelease_WithoutMatch()
	{
		var client = ApiClient();

		var response = await client.GetAsync("/api/v1/parse?title=Series.Title.S01E01.1080p.WEB-DL-GROUP");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await response.Content.ReadFromJsonAsync<ParseResultDto>();
		result!.Parsed.Quality.Resolution.ShouldBe("R1080_P");
		result.Parsed.Quality.Source.ShouldBe("WEB_DL");
		result.Parsed.Series.ShouldNotBeNull();
		result.Match.ShouldBeNull();
	}

	[Fact]
	public async Task Parse_ShouldMatchSeriesAndEpisodes_WhenInLibrary()
	{
		var client = ApiClient();

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
			db.Series.Add(new Core.Entities.Series
			{
				TvdbId = 424_242,
				Title = "Parse Show",
				CleanTitle = "parse show",
				Episodes =
				[
					new Core.Entities.Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" }
				]
			});
			await db.SaveChangesAsync();
		}

		var response = await client.GetAsync("/api/v1/parse?title=Parse.Show.S01E01.720p.HDTV-GROUP");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ParseResultDto>();
		result!.Match.ShouldNotBeNull();
		result.Match!.SeriesId.ShouldNotBeNull();
		result.Match.EpisodeIds.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Parse_ShouldReturn400_WhenTitleIsUnparsable()
	{
		var client = ApiClient();

		(await client.GetAsync("/api/v1/parse?title=absolutely%20not%20a%20release"))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task NamingConfig_GetPut_AndExamples_ShouldWork()
	{
		var client = ApiClient();

		var current = await client.GetFromJsonAsync<NamingConfigDto>("/api/v1/config/naming");
		current!.StandardEpisodeFormat.ShouldNotBeEmpty();

		var updated = current! with { StandardEpisodeFormat = "{Series Title} - {Quality Full}" };
		var put = await client.PutAsJsonAsync("/api/v1/config/naming", updated);
		put.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await put.Content.ReadFromJsonAsync<NamingConfigDto>())!.StandardEpisodeFormat
			.ShouldBe("{Series Title} - {Quality Full}");

		var examples = await client.GetFromJsonAsync<List<NamingSampleDto>>("/api/v1/config/naming/examples");
		examples!.ShouldContain(sample => sample.Name == "StandardEpisodeFormat");
		examples!.ShouldAllBe(sample => !string.IsNullOrWhiteSpace(sample.Preview));

		// restore
		await client.PutAsJsonAsync("/api/v1/config/naming", current);
	}

	[Fact]
	public async Task MediaManagementConfig_GetPut_ShouldRoundTrip()
	{
		var client = ApiClient();

		var current = await client.GetFromJsonAsync<MediaManagementConfigDto>("/api/v1/config/media-management");
		var updated = current! with { DownloadPropersAndRepacks = "DO_NOT_UPGRADE", RecycleBinPath = "/tmp/recycle" };

		var put = await client.PutAsJsonAsync("/api/v1/config/media-management", updated);
		put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
		var saved = await put.Content.ReadFromJsonAsync<MediaManagementConfigDto>();
		saved!.DownloadPropersAndRepacks.ShouldBe("DO_NOT_UPGRADE");

		await client.PutAsJsonAsync("/api/v1/config/media-management", current);
	}

	[Fact]
	public async Task IndexerConfig_GetPut_ShouldRoundTrip()
	{
		var client = ApiClient();

		var current = await client.GetFromJsonAsync<IndexerConfigDto>("/api/v1/config/indexer");
		var updated = current! with { RetentionDays = 42 };

		var put = await client.PutAsJsonAsync("/api/v1/config/indexer", updated);
		put.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await put.Content.ReadFromJsonAsync<IndexerConfigDto>())!.RetentionDays.ShouldBe(42);

		await client.PutAsJsonAsync("/api/v1/config/indexer", current);
	}

	[Fact]
	public async Task IndexerConfig_HardcodedSubsFields_ShouldRoundTrip()
	{
		var client = ApiClient();

		var current = await client.GetFromJsonAsync<IndexerConfigDto>("/api/v1/config/indexer");
		var updated = current! with { AllowHardcodedSubs = true, WhitelistedHardcodedSubs = "FLUX,EVO" };

		var put = await client.PutAsJsonAsync("/api/v1/config/indexer", updated);
		put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
		var saved = await put.Content.ReadFromJsonAsync<IndexerConfigDto>();
		saved!.AllowHardcodedSubs.ShouldBeTrue();
		saved.WhitelistedHardcodedSubs.ShouldBe("FLUX,EVO");

		await client.PutAsJsonAsync("/api/v1/config/indexer", current);
	}

	[Fact]
	public async Task DownloadConfig_GetPut_ShouldRoundTrip()
	{
		var client = ApiClient();

		var current = await client.GetFromJsonAsync<DownloadConfigDto>("/api/v1/config/download");
		var updated = current! with { CheckForFinishedDownloadInterval = 5 };

		var put = await client.PutAsJsonAsync("/api/v1/config/download", updated);
		put.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await put.Content.ReadFromJsonAsync<DownloadConfigDto>())!.CheckForFinishedDownloadInterval.ShouldBe(5);

		await client.PutAsJsonAsync("/api/v1/config/download", current);
	}

	private HttpClient ApiClient()
	{
		_factory.CreateClient().Dispose();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", ReadApiKey());

		return client;
	}

	private string ReadApiKey()
	{
		return _factory.ReadApiKey();
	}

	private static StringContent Json(string payload)
		=> new(payload, Encoding.UTF8, "application/json");

	private sealed record ParseResultDto(ParsedReleaseDto Parsed, ParseMatchDto? Match);

	private sealed record ParseMatchDto(int? SeriesId, int? MovieId, IReadOnlyList<int> EpisodeIds);

	private sealed record ParsedReleaseDto(
		string FullTitle,
		string Title,
		QualityDto Quality,
		SeriesDto? Series,
		MovieDto? Movie);

	private sealed record QualityDto(string? Source, string? Resolution, string Name, int RevisionVersion);

	private sealed record SeriesDto(string ReleaseType, IReadOnlyList<int> Seasons, IReadOnlyList<int> Episodes);

	private sealed record MovieDto(string? Edition);

	private sealed record NamingConfigDto(
		bool RenameEpisodes,
		bool RenameMovies,
		bool ReplaceIllegalCharacters,
		string ColonReplacement,
		string StandardEpisodeFormat,
		string DailyEpisodeFormat,
		string AnimeEpisodeFormat,
		string SeriesFolderFormat,
		string SeasonFolderFormat,
		string SpecialsFolderFormat,
		string MovieFormat,
		string MovieFolderFormat,
		string MultiEpisodeStyle);

	private sealed record NamingSampleDto(string Name, string Preview);

	private sealed record MediaManagementConfigDto(
		bool UseHardlinks,
		bool ImportExtraFiles,
		string ExtraFileExtensions,
		int MinimumFreeSpaceMb,
		bool SkipFreeSpaceCheck,
		string FileDate,
		string RecycleBinPath,
		int RecycleBinCleanupDays,
		bool CreateEmptySeriesFolders,
		bool CreateEmptyMovieFolders,
		bool DeleteEmptyFolders,
		bool UnmonitorDeletedFiles,
		string ChmodFolder,
		string ChmodFile,
		string ChownGroup,
		string DownloadPropersAndRepacks,
		bool EnableMediaInfo);

	private sealed record IndexerConfigDto(
		int RssSyncIntervalMinutes,
		int MinimumAgeMinutes,
		int RetentionDays,
		int MaximumSizeMb,
		int AvailabilityDelayDays,
		bool AllowHardcodedSubs,
		string WhitelistedHardcodedSubs);

	private sealed record DownloadConfigDto(
		bool EnableCompletedDownloadHandling,
		bool RemoveCompletedDownloads,
		bool EnableFailedDownloadHandling,
		bool RedownloadFailedReleases,
		bool RemoveFailedDownloads,
		int CheckForFinishedDownloadInterval);
}
