using Submarine.Core.Entities;
using Submarine.Core.Languages;

namespace Submarine.Core.Profiles;

/// <summary>
///     Language decisions for a <see cref="LanguageProfile" />.
/// </summary>
public static class LanguageProfileExtensions
{
	/// <summary>
	///     Whether any of the languages is wanted by the profile.
	/// </summary>
	public static bool IsWanted(this LanguageProfile profile, IReadOnlyList<Language> languages)
		=> languages.Any(profile.Languages.Contains);

	/// <summary>
	///     Whether the languages contain the profile's cutoff language.
	/// </summary>
	public static bool MeetsCutoff(this LanguageProfile profile, IReadOnlyList<Language> languages)
		=> languages.Contains(profile.Cutoff);

	/// <summary>
	///     Whether the candidate languages rank better than the current ones. A release qualifies when it holds a wanted
	///     language the current file does not have, or a better ranked one.
	/// </summary>
	public static bool IsUpgrade(
		this LanguageProfile profile,
		IReadOnlyList<Language> current,
		IReadOnlyList<Language> candidate)
	{
		if (!profile.UpgradeAllowed)
		{
			return false;
		}

		var candidateRank = BestRank(profile, candidate);
		if (candidateRank < 0)
		{
			return false;
		}

		var currentRank = BestRank(profile, current);
		return currentRank < 0 || candidateRank < currentRank;
	}

	private static int BestRank(LanguageProfile profile, IReadOnlyList<Language> languages)
	{
		var best = -1;

		foreach (var language in languages)
		{
			var rank = profile.Languages.IndexOf(language);

			if (rank >= 0 && (best < 0 || rank < best))
			{
				best = rank;
			}
		}

		return best;
	}
}
