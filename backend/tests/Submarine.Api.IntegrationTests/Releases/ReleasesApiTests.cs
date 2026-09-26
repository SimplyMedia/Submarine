using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Infrastructure.Search;
using Xunit;

namespace Submarine.Api.IntegrationTests.Releases;

public sealed class ReleasesApiTests : IClassFixture<ReleasesApiFactory>
{
	private readonly ReleasesApiFactory _factory;

	public ReleasesApiTests(ReleasesApiFactory factory) => _factory = factory;

	private static (TorrentRelease Release, ReleaseInfo Info) BuildRelease(string guid, int indexerId)
	{
		var release = new TorrentRelease(new BaseRelease
		{
			FullTitle = "Show.S01E01.1080p.WEB-DL-GROUP",
			Title = "Show",
			ReleaseGroup = "GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Languages = [Language.ENGLISH]
		});
		var info = new ReleaseInfo
		{
			Guid = guid,
			Title = release.FullTitle,
			MagnetUrl = "magnet:?xt=urn:btih:abcdef",
			Size = 123456,
			Protocol = Protocol.BITTORRENT,
			IndexerId = indexerId,
			Indexer = "Stub"
		};
		return (release, info);
	}

	[Fact]
	public async Task Grab_ShouldReturnNotFound_WhenMediaVersionBelongsToAnotherSeries()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var (seriesAId, seriesBVersionId) = await _factory.WithDbAsync(async db =>
		{
			var quality = new QualityProfile { Name = "Q1", UpgradeAllowed = true, Cutoff = 0, Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)] };
			var language = new LanguageProfile { Name = "L1", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			db.QualityProfiles.Add(quality);
			db.LanguageProfiles.Add(language);
			var seriesA = new Series { Title = "Ownership A", CleanTitle = "ownershipa", TvdbId = 91001 };
			var seriesB = new Series { Title = "Ownership B", CleanTitle = "ownershipb", TvdbId = 91002 };
			db.Series.AddRange(seriesA, seriesB);
			await db.SaveChangesAsync();
			var versionB = new MediaVersion { SeriesId = seriesB.Id, Name = "Default", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = 0, Path = "b" };
			db.MediaVersions.Add(versionB);
			await db.SaveChangesAsync();
			return (seriesA.Id, versionB.Id);
		});

		var (release, info) = BuildRelease("ownership-guid", 501);
		var cache = _factory.Services.GetRequiredService<ReleaseResultCache>();
		cache.Store(new ReleaseCandidate(release, info, null, seriesAId, null, [1]));

		var response = await client.PostAsJsonAsync("/api/v1/releases/grab", new
		{
			guid = "ownership-guid",
			indexerId = 501,
			mediaVersionId = seriesBVersionId,
			seriesId = seriesAId,
			episodeIds = new[] { 1 }
		});

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Grab_ShouldBypassRejection_WhenOverrideIsSet()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var downloadClient = Substitute.For<IDownloadClient>();
		downloadClient.Protocol.Returns(Protocol.BITTORRENT);
		downloadClient.AddAsync(Arg.Any<RemoteRelease>(), Arg.Any<SeedCriteria?>(), Arg.Any<CancellationToken>()).Returns("OVERRIDDEN");
		_factory.DownloadClientFactory.Create(Arg.Any<DownloadClientType>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(downloadClient);

		var (seriesId, versionId) = await _factory.WithDbAsync(async db =>
		{
			// cutoff at index 0 with the item disallowed: any candidate is permanently rejected unless overridden
			var quality = new QualityProfile
			{
				Name = "Override quality",
				UpgradeAllowed = true,
				Cutoff = 0,
				Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), false)]
			};
			var root = new RootFolder { Path = "/it-root-override", MediaKind = MediaKind.SERIES };
			var language = new LanguageProfile { Name = "Override language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			db.RootFolders.Add(root);
			db.QualityProfiles.Add(quality);
			db.LanguageProfiles.Add(language);
			var series = new Series { Title = "Override Show", CleanTitle = "overrideshow", TvdbId = 91003 };
			db.Series.Add(series);
			await db.SaveChangesAsync();
			var version = new MediaVersion { SeriesId = series.Id, Name = "Default", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "override" };
			db.MediaVersions.Add(version);
			db.DownloadClients.Add(new DownloadClient { Name = "Client", Type = DownloadClientType.QBITTORRENT, Enable = true });
			db.Indexers.Add(new Indexer { Id = 502, Name = "Stub", BaseUrl = "http://x" });
			await db.SaveChangesAsync();
			return (series.Id, version.Id);
		});

		var (release, info) = BuildRelease("override-guid", 502);
		var cache = _factory.Services.GetRequiredService<ReleaseResultCache>();
		cache.Store(new ReleaseCandidate(release, info, null, seriesId, null, [1]));

		var withoutOverride = await client.PostAsJsonAsync("/api/v1/releases/grab", new
		{
			guid = "override-guid",
			indexerId = 502,
			mediaVersionId = versionId,
			seriesId,
			episodeIds = new[] { 1 }
		});
		withoutOverride.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		var withOverride = await client.PostAsJsonAsync("/api/v1/releases/grab", new
		{
			guid = "override-guid",
			indexerId = 502,
			mediaVersionId = versionId,
			seriesId,
			episodeIds = new[] { 1 },
			@override = true
		});
		withOverride.StatusCode.ShouldBe(HttpStatusCode.OK);

		var tracked = await _factory.WithDbAsync(db => db.TrackedDownloads.SingleAsync(row => row.MediaVersionId == versionId));
		tracked.DownloadId.ShouldBe("OVERRIDDEN");
	}
}
