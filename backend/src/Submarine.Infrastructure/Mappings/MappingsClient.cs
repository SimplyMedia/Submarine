using System.Net.Http.Json;
using Submarine.Contracts.Mappings;
using Submarine.Core.Common;

namespace Submarine.Infrastructure.Mappings;

/// <summary>
///     HTTP implementation of <see cref="IMappingsClient" /> against the Mappings service.
/// </summary>
public sealed class MappingsClient(HttpClient http) : IMappingsClient
{
	public async Task<SceneMappingSetResource> GetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> await http.GetFromJsonAsync<SceneMappingSetResource>($"/api/v1/scene/{tvdbId}", SubmarineJson.Default, cancellationToken)
			?? new SceneMappingSetResource(tvdbId, [], []);

	public async Task<SceneResolution> ResolveSceneAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default)
		=> await GetAsync<SceneResolution>($"/api/v1/scene/{tvdbId}/resolve?season={season}&episode={episode}", cancellationToken)
			?? new SceneResolution(season, episode);

	public async Task<TvdbResolution> ResolveTvdbAsync(int tvdbId, int sceneSeason, int sceneEpisode, CancellationToken cancellationToken = default)
		=> await GetAsync<TvdbResolution>($"/api/v1/scene/{tvdbId}/resolve-tvdb?sceneSeason={sceneSeason}&sceneEpisode={sceneEpisode}", cancellationToken)
			?? new TvdbResolution(tvdbId, sceneSeason, sceneEpisode, 0);

	public async Task<IReadOnlyList<SceneNameResource>> GetSceneNamesAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> await http.GetFromJsonAsync<List<SceneNameResource>>($"/api/v1/scene/names/{tvdbId}", SubmarineJson.Default, cancellationToken) ?? [];

	public async Task<IReadOnlyList<int>> FindByNameAsync(string name, CancellationToken cancellationToken = default)
		=> await http.GetFromJsonAsync<List<int>>($"/api/v1/scene/search?name={Uri.EscapeDataString(name)}", SubmarineJson.Default, cancellationToken) ?? [];

	public async Task<IReadOnlyList<AniListMappingResource>> GetAniListMappingsAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> await http.GetFromJsonAsync<List<AniListMappingResource>>($"/api/v1/tvdb/{tvdbId}/anilist", SubmarineJson.Default, cancellationToken) ?? [];

	public async Task<AniListResolution> ResolveAniListAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default)
		=> await GetAsync<AniListResolution>($"/api/v1/tvdb/{tvdbId}/anilist/resolve?season={season}&episode={episode}", cancellationToken)
			?? new AniListResolution(0, 0);

	private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
		=> await http.GetFromJsonAsync<T>(url, SubmarineJson.Default, cancellationToken);
}
