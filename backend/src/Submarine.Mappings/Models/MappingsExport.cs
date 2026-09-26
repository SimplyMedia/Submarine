namespace Submarine.Mappings.Models;

/// <summary>
/// The complete mappings dataset, used by the export and import endpoints.
/// </summary>
public sealed record MappingsExport(
	IReadOnlyList<Contracts.Mappings.SceneMappingResource> SceneMappings,
	IReadOnlyList<Contracts.Mappings.SceneEpisodeMappingResource> EpisodeMappings,
	IReadOnlyList<Contracts.Mappings.AniListMappingResource> AniListMappings,
	IReadOnlyList<Contracts.Mappings.SceneNameResource> SceneNames);

/// <summary>
/// Row counts applied by a successful import.
/// </summary>
public sealed record ImportResult(int SceneMappings, int EpisodeMappings, int AniListMappings, int SceneNames);
