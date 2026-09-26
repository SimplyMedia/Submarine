namespace Submarine.Contracts.Mappings;

/// <summary>
/// Maps one AniList entry (arc or cour) onto a TVDB season starting at EpisodeStart.
/// A null EpisodeCount means the arc is open ended.
/// </summary>
public sealed record AniListMappingResource(
	int Id,
	int AniListId,
	int TvdbId,
	string Title,
	int TvdbSeason,
	int EpisodeStart,
	int? EpisodeCount,
	int AbsoluteOffset);

public sealed record CreateAniListMappingRequest(
	int AniListId,
	int TvdbId,
	string Title,
	int TvdbSeason,
	int EpisodeStart,
	int? EpisodeCount,
	int AbsoluteOffset);

public sealed record UpdateAniListMappingRequest(
	int AniListId,
	int TvdbId,
	string Title,
	int TvdbSeason,
	int EpisodeStart,
	int? EpisodeCount,
	int AbsoluteOffset);
