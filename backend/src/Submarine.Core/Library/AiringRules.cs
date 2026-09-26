using Submarine.Core.Entities;

namespace Submarine.Core.Library;

/// <summary>
///     Computes the next and previous airing episode of a series.
/// </summary>
public static class AiringRules
{
	/// <summary>
	///     Earliest monitored episode airing in the future, null when none.
	/// </summary>
	public static Episode? NextAiring(IEnumerable<Episode> episodes, DateTime now)
		=> episodes
			.Where(x => x.Monitored && x.AirDateUtc is not null && x.AirDateUtc > now)
			.OrderBy(x => x.AirDateUtc)
			.FirstOrDefault();

	/// <summary>
	///     Latest monitored episode already aired, null when none.
	/// </summary>
	public static Episode? PreviousAiring(IEnumerable<Episode> episodes, DateTime now)
		=> episodes
			.Where(x => x.Monitored && x.AirDateUtc is not null && x.AirDateUtc <= now)
			.OrderByDescending(x => x.AirDateUtc)
			.FirstOrDefault();
}
