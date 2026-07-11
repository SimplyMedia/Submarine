namespace Submarine.Mappings.Contracts;

/// <summary>
///     Maps a single AniList entry (one arc/cour/part) onto a TVDB season range
/// </summary>
/// <param name="AniListId">AniList identifier of the entry this mapping applies to</param>
/// <param name="TvdbId">TheTVDB identifier of the series this entry belongs to</param>
/// <param name="Title">title of the AniList entry</param>
/// <param name="TvdbSeason">TVDB season this entry maps into</param>
/// <param name="EpisodeStart">first TVDB episode of that season covered by this entry, 1-based</param>
/// <param name="EpisodeCount">number of episodes this entry covers, or null when open-ended</param>
/// <param name="AbsoluteOffset">value to add to an AniList episode number to get the TVDB absolute number</param>
public record AniListMappingResource(
	int AniListId,
	int TvdbId,
	string Title,
	int TvdbSeason,
	int EpisodeStart,
	int? EpisodeCount,
	int AbsoluteOffset);
