using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Core.Library;

/// <summary>
///     Whether a movie may be grabbed considering its release dates and the minimum availability.
/// </summary>
public static class MovieAvailability
{
	/// <summary>
	///     A movie is available when it reached the configured minimum availability,
	///     minus an optional delay in days.
	/// </summary>
	public static bool IsAvailable(Movie movie, MinimumAvailability minimumAvailability, int availabilityDelayDays, DateTime now)
	{
		var date = minimumAvailability switch
		{
			MinimumAvailability.ANNOUNCED => MovieDate(movie),
			MinimumAvailability.IN_CINEMAS => movie.InCinemasDate,
			MinimumAvailability.RELEASED => movie.PhysicalReleaseDate ?? movie.DigitalReleaseDate ?? movie.InCinemasDate,
			_ => null
		};

		if (date is null)
		{
			return false;
		}

		return date.Value.AddDays(availabilityDelayDays) <= now;
	}

	private static DateTime? MovieDate(Movie movie)
		=> movie.PhysicalReleaseDate
			?? movie.DigitalReleaseDate
			?? movie.InCinemasDate;
}
