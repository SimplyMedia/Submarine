using Submarine.Mappings.Contracts;

namespace Submarine.Mappings.Models;

/// <summary>
///     All scene mappings for a series, including per-episode overrides
/// </summary>
/// <param name="Mappings">season-level (or all-seasons) mappings</param>
/// <param name="EpisodeMappings">per-episode overrides</param>
public record SceneMappingSetResource(
	IReadOnlyList<SceneMappingResource> Mappings,
	IReadOnlyList<SceneEpisodeMappingResource> EpisodeMappings);
