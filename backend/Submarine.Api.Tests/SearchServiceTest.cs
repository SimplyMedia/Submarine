using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.DecisionEngine;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Library;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
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

	private async Task<(Series Series, Episode Episode)> SeedSeriesAsync(SeriesType type)
	{
		Context.QualityProfiles.Add(new QualityProfile { Id = 1, Name = "Any" });
		Context.LanguageProfiles.Add(new LanguageProfile { Id = 1, Name = "Any" });

		Context.Providers.Add(new TorznabIndexer
		{
			Name = "Indexer", Url = "http://indexer/", ApiKey = "key", Mode = ProviderMode.AUTOMATIC_SEARCH,
			Tags = new List<string>(), Categories = new List<int> { 5000 }, AnimeCategories = new List<int> { 5070 }
		});

		var series = new Series
		{
			TvdbId = 42, Title = "Show", Path = "/lib/Show", Monitored = true, Type = type,
			QualityProfileId = 1, LanguageProfileId = 1
		};
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

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
