using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.DecisionEngine;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Indexer;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Mappings.Contracts;
using Xunit;

namespace Submarine.Api.Tests;

public class SearchServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task SearchEpisodeAsync_ShouldIssueSceneNumberedQuery_WhenSceneMappingHasOffset()
	{
		var (series, episode) = await SeedSeriesAsync(SeriesType.STANDARD);

		var torznab = new FakeTorznabSearchClient();
		var mappings = new FakeMappingsClient
		{
			SceneMappings = new SceneMappingSet(
				new[] { new SceneMappingResource(series.TvdbId, "Scene Title", SeasonNumber: 1, SceneSeasonNumber: 1,
					EpisodeOffset: 100) },
				Array.Empty<SceneEpisodeMappingResource>())
		};

		var service = BuildService(torznab, mappings);

		await service.SearchEpisodeAsync(series.Id, 1, 5);

		Assert.Contains(torznab.TvSearches, s => s is { Season: 1, Episode: 5, Query: null });
		Assert.Contains(torznab.TvSearches, s => s is { Season: 1, Episode: 105, Query: "Scene Title" });
	}

	[Fact]
	public async Task SearchEpisodeAsync_ShouldStillQueryBaseNumbering_WhenMappingsServiceUnreachable()
	{
		var (series, episode) = await SeedSeriesAsync(SeriesType.STANDARD);

		var torznab = new FakeTorznabSearchClient();
		var mappings = new FakeMappingsClient { Unreachable = true };

		var service = BuildService(torznab, mappings);

		var result = await service.SearchEpisodeAsync(series.Id, 1, 5);

		Assert.Empty(result);
		Assert.Contains(torznab.TvSearches, s => s is { Season: 1, Episode: 5, Query: null });
		Assert.DoesNotContain(torznab.TvSearches, s => s.Season == 105 || s.Episode == 105);
	}

	[Fact]
	public async Task SearchEpisodeAsync_ShouldDecidePerVersionWithOwnExistingFile_WhenTwoVersionsMonitored()
	{
		Context.QualityProfiles.Add(WebDl1080Profile(1));
		Context.QualityProfiles.Add(WebDl1080Profile(2));
		Context.LanguageProfiles.Add(new LanguageProfile
		{
			Id = 1, Name = "English", Languages = new List<Language> { Language.ENGLISH }, Cutoff = Language.ENGLISH
		});

		Context.Providers.Add(new TorznabIndexer
		{
			Name = "Indexer", Url = "http://indexer/", ApiKey = "key", Mode = ProviderMode.AUTOMATIC_SEARCH,
			Tags = new List<string>(), Categories = new List<int> { 5000 }, AnimeCategories = new List<int> { 5070 }
		});

		var series = new Series { TvdbId = 42, Title = "Show", Monitored = true, Type = SeriesType.STANDARD };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var versionA = new MediaVersion
		{
			SeriesId = series.Id, Name = "1080p", Path = "/lib/1080p/Show", QualityProfileId = 1,
			LanguageProfileId = 1, Monitored = true
		};
		var versionB = new MediaVersion
		{
			SeriesId = series.Id, Name = "4K", Path = "/lib/4K/Show", QualityProfileId = 2, LanguageProfileId = 1,
			Monitored = true
		};
		Context.Versions.AddRange(versionA, versionB);
		var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5 };
		Context.Episodes.Add(episode);
		await Context.SaveChangesAsync();

		// Version A already holds a 1080p file; version B holds nothing.
		Context.EpisodeFiles.Add(new EpisodeFile
		{
			SeriesId = series.Id, MediaVersionId = versionA.Id, RelativePath = "Season 01/Show - S01E05.mkv",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision()),
			Languages = new List<Language> { Language.ENGLISH }, Episodes = new List<Episode> { episode }
		});
		await Context.SaveChangesAsync();

		var torznab = new FakeTorznabSearchClient
		{
			Result = new[]
			{
				new ReleaseInfo
				{
					Title = "Show S01E05 1080p WEB-DL x264-GROUP", Guid = "guid-1",
					DownloadUrl = "http://indexer/download", Protocol = Protocol.BITTORRENT
				}
			}
		};

		var service = BuildService(torznab, new FakeMappingsClient());

		var decisions = await service.SearchEpisodeAsync(series.Id, 1, 5);

		Assert.Equal(new[] { versionA.Id, versionB.Id }.OrderBy(id => id),
			decisions.Select(d => d.VersionId!.Value).Distinct().OrderBy(id => id));

		var forA = decisions.Single(d => d.VersionId == versionA.Id);
		var forB = decisions.Single(d => d.VersionId == versionB.Id);

		Assert.False(forA.Decision.Approved);
		Assert.True(forB.Decision.Approved);
	}

	private static QualityProfile WebDl1080Profile(int id)
		=> new()
		{
			Id = id,
			Name = $"Profile {id}",
			Cutoff = 0,
			Items = new List<QualityProfileItem>
			{
				new()
				{
					Quality = new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
					Allowed = true
				}
			}
		};

	private async Task<(Series Series, Episode Episode)> SeedSeriesAsync(SeriesType type)
	{
		Context.QualityProfiles.Add(new QualityProfile { Id = 1, Name = "Any" });
		Context.LanguageProfiles.Add(new LanguageProfile { Id = 1, Name = "Any" });

		Context.Providers.Add(new TorznabIndexer
		{
			Name = "Indexer", Url = "http://indexer/", ApiKey = "key", Mode = ProviderMode.AUTOMATIC_SEARCH,
			Tags = new List<string>(), Categories = new List<int> { 5000 }, AnimeCategories = new List<int> { 5070 }
		});

		var series = new Series { TvdbId = 42, Title = "Show", Monitored = true, Type = type };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		Context.Versions.Add(new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = "/lib/Show", QualityProfileId = 1, LanguageProfileId = 1,
			Monitored = true
		});
		var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5 };
		Context.Episodes.Add(episode);
		await Context.SaveChangesAsync();

		return (series, episode);
	}

	private SearchService BuildService(ITorznabSearchClient torznab, IMappingsClient mappings)
	{
		var decision = new DownloadDecisionService(NullLogger<DownloadDecisionService>.Instance,
			new FilterEvaluator(), new CustomFormatEvaluator(NullLogger<CustomFormatEvaluator>.Instance));

		return new SearchService(NullLogger<SearchService>.Instance, new ProviderRepository(Context),
			new SeriesRepository(Context), new MovieRepository(Context), new QualityProfileRepository(Context),
			new LanguageProfileRepository(Context), new ReleaseFilterRepository(Context),
			new CustomFormatRepository(Context), torznab, mappings, ReleaseParser(), decision);
	}
}
