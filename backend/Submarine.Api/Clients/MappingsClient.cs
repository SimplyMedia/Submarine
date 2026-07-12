using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Mappings.Contracts;

namespace Submarine.Api.Clients;

/// <summary>
///     Http implementation of <see cref="IMappingsClient" />
/// </summary>
public class MappingsClient : IMappingsClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly HttpClient _httpClient;

	/// <summary>
	///     Creates a new instance of <see cref="MappingsClient" />
	/// </summary>
	/// <param name="httpClient">Http client to resolve mappings with</param>
	public MappingsClient(HttpClient httpClient)
		=> _httpClient = httpClient;

	/// <inheritdoc />
	public async Task<SceneMappingSet?> GetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken = default)
	{
		using var response = await _httpClient.GetAsync($"api/v1/scenemapping/{tvdbId}", cancellationToken);

		if (response.StatusCode == HttpStatusCode.NotFound)
			return null;

		response.EnsureSuccessStatusCode();

		var set = await response.Content.ReadFromJsonAsync<SceneMappingSet>(JsonOptions, cancellationToken);

		return set is { Mappings.Count: 0, EpisodeMappings.Count: 0 } ? null : set;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<AniListMappingResource>> GetAniListMappingsAsync(int tvdbId,
		CancellationToken cancellationToken = default)
	{
		var results = await _httpClient.GetFromJsonAsync<IReadOnlyList<AniListMappingResource>>(
			$"api/v1/tvdb/{tvdbId}/anilist", JsonOptions, cancellationToken);

		return results ?? Array.Empty<AniListMappingResource>();
	}

	/// <inheritdoc />
	public async Task<AniListResolution?> ResolveAniListAsync(int tvdbId, int season, int episode,
		CancellationToken cancellationToken = default)
	{
		using var response = await _httpClient.GetAsync(
			$"api/v1/tvdb/{tvdbId}/anilist/resolve?season={season}&episode={episode}", cancellationToken);

		if (response.StatusCode == HttpStatusCode.NotFound)
			return null;

		response.EnsureSuccessStatusCode();

		return await response.Content.ReadFromJsonAsync<AniListResolution>(JsonOptions, cancellationToken);
	}
}
