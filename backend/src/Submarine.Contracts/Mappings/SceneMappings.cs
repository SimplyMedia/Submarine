namespace Submarine.Contracts.Mappings;

/// <summary>
/// A scene-to-TVDB season level mapping. A null SeasonNumber is an all-seasons wildcard.
/// A null SceneSeasonNumber keeps the TVDB season number.
/// </summary>
public sealed record SceneMappingResource(
	int Id,
	int TvdbId,
	string Title,
	int? SeasonNumber,
	int? SceneSeasonNumber,
	int EpisodeOffset,
	string? SearchTitle,
	string? Comment);

/// <summary>
/// An exact single episode scene mapping that overrides any season level mapping.
/// </summary>
public sealed record SceneEpisodeMappingResource(
	int Id,
	int TvdbId,
	int SeasonNumber,
	int EpisodeNumber,
	int SceneSeasonNumber,
	int SceneEpisodeNumber);

/// <summary>
/// The complete scene mapping dataset for one TVDB series.
/// </summary>
public sealed record SceneMappingSetResource(
	int TvdbId,
	IReadOnlyList<SceneMappingResource> Mappings,
	IReadOnlyList<SceneEpisodeMappingResource> EpisodeMappings);

public sealed record CreateSceneMappingRequest(
	int TvdbId,
	string Title,
	int? SeasonNumber,
	int? SceneSeasonNumber,
	int EpisodeOffset,
	string? SearchTitle,
	string? Comment);

public sealed record UpdateSceneMappingRequest(
	int TvdbId,
	string Title,
	int? SeasonNumber,
	int? SceneSeasonNumber,
	int EpisodeOffset,
	string? SearchTitle,
	string? Comment);

public sealed record CreateSceneEpisodeMappingRequest(
	int TvdbId,
	int SeasonNumber,
	int EpisodeNumber,
	int SceneSeasonNumber,
	int SceneEpisodeNumber);

public sealed record UpdateSceneEpisodeMappingRequest(
	int TvdbId,
	int SeasonNumber,
	int EpisodeNumber,
	int SceneSeasonNumber,
	int SceneEpisodeNumber);
