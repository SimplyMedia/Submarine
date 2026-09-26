using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Submarine.Contracts.Mappings;
using Submarine.Infrastructure.Mappings;

namespace Submarine.Api.IntegrationTests.Indexers;

/// <summary>
///     Hosts the real API against a temporary Sqlite database for indexer/search/grab/newznab integration tests.
/// </summary>
public sealed class IndexersApiFactory : SubmarineApiFactory
{
	protected override void ConfigureTestServices(IServiceCollection services)
	{
		services.RemoveAll<IMappingsClient>();
		services.AddSingleton<IMappingsClient, EmptyMappingsClient>();
	}

	private sealed class EmptyMappingsClient : IMappingsClient
	{
		public Task<SceneMappingSetResource> GetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken = default)
			=> Task.FromResult(new SceneMappingSetResource(tvdbId, [], []));

		public Task<SceneResolution> ResolveSceneAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default)
			=> Task.FromResult(new SceneResolution(season, episode));

		public Task<TvdbResolution> ResolveTvdbAsync(int tvdbId, int sceneSeason, int sceneEpisode, CancellationToken cancellationToken = default)
			=> Task.FromResult(new TvdbResolution(tvdbId, sceneSeason, sceneEpisode, sceneEpisode));

		public Task<IReadOnlyList<SceneNameResource>> GetSceneNamesAsync(int tvdbId, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<SceneNameResource>>([]);

		public Task<IReadOnlyList<int>> FindByNameAsync(string name, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<int>>([]);

		public Task<IReadOnlyList<AniListMappingResource>> GetAniListMappingsAsync(int tvdbId, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<AniListMappingResource>>([]);

		public Task<AniListResolution> ResolveAniListAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default)
			=> Task.FromResult(new AniListResolution(0, episode));
	}
}
