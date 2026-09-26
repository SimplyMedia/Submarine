using Submarine.Core.Library;

namespace Submarine.Core.Search;

/// <summary>
///     Matches a parsed release title against a library item's known titles, disambiguating by year when a year is
///     known on both sides.
/// </summary>
public static class TitleMatcher
{
	/// <summary>
	///     Whether the release matches one of the candidate titles and, when years are known, they agree within one
	///     year of tolerance (air date year rollovers).
	/// </summary>
	public static bool IsMatch(string releaseTitle, int? releaseYear, IReadOnlyList<string> candidateTitles, int? candidateYear)
	{
		var clean = TitleNormalizer.CleanTitle(releaseTitle);
		if (!candidateTitles.Any(candidate => TitleNormalizer.CleanTitle(candidate) == clean))
		{
			return false;
		}

		return releaseYear is null || candidateYear is null || Math.Abs(releaseYear.Value - candidateYear.Value) <= 1;
	}
}
