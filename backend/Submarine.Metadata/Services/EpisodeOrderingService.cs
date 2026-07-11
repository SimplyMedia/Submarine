using Submarine.Metadata.Clients;
using Submarine.Metadata.Contracts;

namespace Submarine.Metadata.Services;

/// <summary>
///     Normalizes raw TVDB episodes into <see cref="EpisodeResource" /> entries carrying every available ordering
/// </summary>
internal static class EpisodeOrderingService
{
	/// <summary>
	///     Normalizes the given raw TVDB episodes, attaching an <see cref="EpisodeNumber" /> per available ordering.
	///     Episodes lacking dvd or absolute data omit those orderings.
	/// </summary>
	/// <param name="episodes">raw TVDB episodes</param>
	/// <returns>normalized episodes</returns>
	public static IReadOnlyList<EpisodeResource> Normalize(IEnumerable<TvdbEpisode> episodes)
		=> episodes.Select(Map).ToList();

	private static EpisodeResource Map(TvdbEpisode episode)
	{
		var numbers = new List<EpisodeNumber>
		{
			new(EpisodeOrdering.Aired, episode.SeasonNumber, episode.Number, null)
		};

		if (episode.DvdSeason != null && episode.DvdEpisode != null)
			numbers.Add(new EpisodeNumber(EpisodeOrdering.Dvd, episode.DvdSeason, episode.DvdEpisode, null));

		if (episode.AbsoluteNumber is > 0)
			numbers.Add(new EpisodeNumber(EpisodeOrdering.Absolute, null, null, episode.AbsoluteNumber));

		return new EpisodeResource(
			episode.Id,
			null,
			episode.Name,
			episode.Overview,
			DateOnly.TryParse(episode.Aired, out var aired) ? aired : null,
			episode.Runtime,
			numbers);
	}
}
