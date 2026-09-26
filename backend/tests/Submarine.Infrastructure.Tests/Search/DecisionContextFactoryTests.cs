using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class DecisionContextFactoryTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly DecisionContextFactory _factory;

	public DecisionContextFactoryTests()
	{
		_db = TestDbFactory.Create(_clock);
		_factory = new DecisionContextFactory(_db, new BlocklistService(_db));
	}

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task BuildAsync_ShouldCompareAgainstWorstFile_WhenSeasonPackCoversPartiallyCoveredEpisodes()
	{
		var qualityProfile = new QualityProfile
		{
			Name = "Season pack profile",
			Cutoff = 1,
			Items =
			[
				new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P), true),
				new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)
			]
		};
		var languageProfile = new LanguageProfile { Name = "L1", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
		var series = new Series { TvdbId = 930001, Title = "Pack series" };
		_db.AddRange(qualityProfile, languageProfile, series);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var episodeLow = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true };
		var episodeHigh = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 2, Monitored = true };
		_db.Episodes.AddRange(episodeLow, episodeHigh);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var version = new MediaVersion { Name = "Main", SeriesId = series.Id, QualityProfileId = qualityProfile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = 0, Path = "Pack series" };
		_db.MediaVersions.Add(version);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var lowFile = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "low.mkv",
			DateAdded = _clock.GetUtcNow().UtcDateTime,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P), new Revision()),
			Episodes = [episodeLow]
		};
		var highFile = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "high.mkv",
			DateAdded = _clock.GetUtcNow().UtcDateTime,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Episodes = [episodeHigh]
		};
		_db.EpisodeFiles.AddRange(lowFile, highFile);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var context = await _factory.BuildAsync(version, [episodeLow.Id, episodeHigh.Id], cancellationToken: TestContext.Current.CancellationToken);

		context.ExistingFileQuality!.Resolution.Resolution.ShouldBe(QualityResolution.R720_P);
		context.ExistingFileCoversMoreEpisodes.ShouldBeFalse();
	}

	[Fact]
	public async Task BuildAsync_ShouldFlagCoveringMoreEpisodes_WhenExistingFileExtendsOutsideCandidateSet()
	{
		var qualityProfile = new QualityProfile { Name = "P", Cutoff = 0, Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)] };
		var languageProfile = new LanguageProfile { Name = "L", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
		var series = new Series { TvdbId = 930002, Title = "Wide file series" };
		_db.AddRange(qualityProfile, languageProfile, series);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var candidateEpisode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true };
		var otherEpisode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 2, Monitored = true };
		_db.Episodes.AddRange(candidateEpisode, otherEpisode);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var version = new MediaVersion { Name = "Main", SeriesId = series.Id, QualityProfileId = qualityProfile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = 0, Path = "Wide" };
		_db.MediaVersions.Add(version);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var seasonPackFile = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "pack.mkv",
			DateAdded = _clock.GetUtcNow().UtcDateTime,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Episodes = [candidateEpisode, otherEpisode]
		};
		_db.EpisodeFiles.Add(seasonPackFile);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var context = await _factory.BuildAsync(version, [candidateEpisode.Id], cancellationToken: TestContext.Current.CancellationToken);

		context.ExistingFileCoversMoreEpisodes.ShouldBeTrue();
	}

	[Fact]
	public async Task BuildAsync_ShouldIncludeQueuedReleaseWithComputedScore_ForInFlightDownloadOnSameVersion()
	{
		var qualityProfile = new QualityProfile { Name = "P2", Cutoff = 0, Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)] };
		var languageProfile = new LanguageProfile { Name = "L2", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
		var movie = new Movie { TmdbId = 930003, Title = "Queued movie" };
		_db.AddRange(qualityProfile, languageProfile, movie);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var version = new MediaVersion { Name = "Main", MovieId = movie.Id, QualityProfileId = qualityProfile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = 0, Path = "Queued" };
		_db.MediaVersions.Add(version);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var downloadClient = new DownloadClient { Name = "Client" };
		_db.DownloadClients.Add(downloadClient);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		_db.TrackedDownloads.Add(new TrackedDownload
		{
			DownloadClientId = downloadClient.Id,
			DownloadId = "queued-1",
			Title = "queued.release",
			MovieId = movie.Id,
			MediaVersionId = version.Id,
			Protocol = Protocol.BITTORRENT,
			State = TrackedDownloadState.DOWNLOADING,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Added = _clock.GetUtcNow().UtcDateTime
		});
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var context = await _factory.BuildAsync(version, cancellationToken: TestContext.Current.CancellationToken);

		context.QueuedReleases.Count.ShouldBe(1);
		context.QueuedReleases.Single().MovieId.ShouldBe(movie.Id);
	}

	[Theory]
	[InlineData(MinimumAvailability.RELEASED, -5, true)]
	[InlineData(MinimumAvailability.RELEASED, 5, false)]
	public async Task BuildAsync_ShouldComputeMinimumAvailabilityMet_BasedOnPhysicalReleaseDate(
		MinimumAvailability minimumAvailability, int releaseDateOffsetDays, bool expected)
	{
		var qualityProfile = new QualityProfile { Name = "P3", Cutoff = 0, Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)] };
		var languageProfile = new LanguageProfile { Name = "L3", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
		var movie = new Movie
		{
			TmdbId = 930004,
			Title = "Availability movie",
			MinimumAvailability = minimumAvailability,
			PhysicalReleaseDate = DateTime.UtcNow.AddDays(releaseDateOffsetDays)
		};
		_db.AddRange(qualityProfile, languageProfile, movie);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var version = new MediaVersion { Name = "Main", MovieId = movie.Id, QualityProfileId = qualityProfile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = 0, Path = "Availability" };
		_db.MediaVersions.Add(version);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var context = await _factory.BuildAsync(version, cancellationToken: TestContext.Current.CancellationToken);

		context.MinimumAvailabilityMet.ShouldBe(expected);
	}
}
