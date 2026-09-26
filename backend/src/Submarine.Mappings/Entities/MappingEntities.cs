namespace Submarine.Mappings.Entities;

/// <summary>
/// A scene to TVDB season level mapping. A null SeasonNumber applies to all seasons.
/// A null SceneSeasonNumber keeps the TVDB season number.
/// </summary>
public sealed class SceneMapping
{
	public int Id { get; set; }

	public int TvdbId { get; set; }

	public string Title { get; set; } = string.Empty;

	public int? SeasonNumber { get; set; }

	public int? SceneSeasonNumber { get; set; }

	public int EpisodeOffset { get; set; }

	public string? SearchTitle { get; set; }

	public string? Comment { get; set; }
}

/// <summary>
/// An exact single episode mapping that overrides any season level mapping.
/// </summary>
public sealed class SceneEpisodeMapping
{
	public int Id { get; set; }

	public int TvdbId { get; set; }

	public int SeasonNumber { get; set; }

	public int EpisodeNumber { get; set; }

	public int SceneSeasonNumber { get; set; }

	public int SceneEpisodeNumber { get; set; }
}

/// <summary>
/// Maps one AniList entry (arc or cour) onto a TVDB season. A null EpisodeCount is open ended.
/// </summary>
public sealed class AniListMapping
{
	public int Id { get; set; }

	public int AniListId { get; set; }

	public int TvdbId { get; set; }

	public string Title { get; set; } = string.Empty;

	public int TvdbSeason { get; set; }

	public int EpisodeStart { get; set; }

	public int? EpisodeCount { get; set; }

	public int AbsoluteOffset { get; set; }
}

/// <summary>
/// An alternate scene title for a TVDB series, used for release title matching.
/// </summary>
public sealed class SceneNameEntry
{
	public int Id { get; set; }

	public int TvdbId { get; set; }

	public string SceneName { get; set; } = string.Empty;

	public int? SeasonNumber { get; set; }
}
