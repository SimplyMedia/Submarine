using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Quality;

namespace Submarine.Core.Profiles;

/// <summary>
///     Quality decisions for a <see cref="QualityProfile" />: tier lookup, cutoff checks and upgrade rules.
/// </summary>
public static class QualityProfileExtensions
{
	/// <summary>
	///     Index of the profile item matching the quality's source and resolution, -1 when the quality is not in the profile.
	/// </summary>
	public static int GetIndex(this QualityProfile profile, QualityModel quality)
		=> profile.Items.FindIndex(item =>
			item.Quality.Source == quality.Resolution.Source
			&& item.Quality.Resolution == quality.Resolution.Resolution);

	/// <summary>
	///     Whether the quality is part of the profile and allowed to be downloaded.
	/// </summary>
	public static bool IsAllowed(this QualityProfile profile, QualityModel quality)
	{
		var index = profile.GetIndex(quality);
		return index >= 0 && profile.Items[index].Allowed;
	}

	/// <summary>
	///     Whether the quality sits at or above the profile's cutoff.
	/// </summary>
	public static bool MeetsCutoff(this QualityProfile profile, QualityModel quality)
	{
		var index = profile.GetIndex(quality);
		return index >= 0 && index >= profile.Cutoff;
	}

	/// <summary>
	///     Whether the quality sits at or above the profile's cutoff and, once there, the held custom format score has
	///     reached <see cref="QualityProfile.CutoffFormatScore" />. A zero <see cref="QualityProfile.CutoffFormatScore" />
	///     opts out of the format score check, matching pre-existing profiles.
	/// </summary>
	public static bool MeetsCutoff(this QualityProfile profile, QualityModel quality, int currentFormatScore)
		=> profile.MeetsCutoff(quality) && currentFormatScore >= profile.CutoffFormatScore;

	/// <summary>
	///     Compares two qualities by profile tier, then by revision. A positive result means left is preferred.
	/// </summary>
	public static int Compare(this QualityProfile profile, QualityModel left, QualityModel right)
	{
		var leftIndex = profile.GetIndex(left);
		var rightIndex = profile.GetIndex(right);
		if (leftIndex != rightIndex)
		{
			return leftIndex.CompareTo(rightIndex);
		}

		return left.Revision.CompareTo(right.Revision);
	}

	/// <summary>
	///     Whether the candidate should replace the current file. A higher allowed tier is an upgrade once the custom format
	///     score reaches <see cref="QualityProfile.MinUpgradeFormatScore" />. On the same tier only a higher revision is an
	///     upgrade, and only when propers and repacks are preferred.
	/// </summary>
	/// <param name="profile">The profile deciding.</param>
	/// <param name="current">Quality of the currently held file.</param>
	/// <param name="candidate">Quality of the candidate release.</param>
	/// <param name="currentScore">Custom format score of the currently held file.</param>
	/// <param name="candidateScore">Custom format score of the candidate release.</param>
	/// <param name="upgradeAllowed">Whether upgrades are allowed for this decision.</param>
	/// <param name="downloadPropersAndRepacks">How propers and repacks are treated.</param>
	public static bool IsUpgrade(
		this QualityProfile profile,
		QualityModel current,
		QualityModel candidate,
		int currentScore,
		int candidateScore,
		bool upgradeAllowed,
		DownloadPropersAndRepacks downloadPropersAndRepacks = DownloadPropersAndRepacks.PREFER_AND_UPGRADE)
	{
		if (!upgradeAllowed || profile.MeetsCutoff(current, currentScore))
		{
			return false;
		}

		var candidateIndex = profile.GetIndex(candidate);
		if (candidateIndex < 0 || !profile.Items[candidateIndex].Allowed)
		{
			return false;
		}

		var currentIndex = profile.GetIndex(current);

		if (candidateIndex > currentIndex)
		{
			return candidateScore >= profile.MinUpgradeFormatScore;
		}

		if (candidateIndex < currentIndex)
		{
			return false;
		}

		return IsRevisionUpgrade(current.Revision, candidate.Revision, downloadPropersAndRepacks)
		       || IsFormatScoreUpgrade(profile, currentScore, candidateScore);
	}

	/// <summary>
	///     Whether the candidate should replace the current file, combining the same-tier proper/repack bypass (a
	///     revision upgrade at the same quality tier is always allowed, even once the profile's cutoff is met) with
	///     <see cref="IsUpgrade" />. Shared between the decision engine and import so a grabbed proper, repack or custom
	///     format upgrade past the quality cutoff imports instead of being rejected as same or worse quality.
	/// </summary>
	public static bool IsQualityUpgrade(
		this QualityProfile profile,
		QualityModel current,
		QualityModel candidate,
		int currentScore,
		int candidateScore,
		bool upgradeAllowed,
		DownloadPropersAndRepacks downloadPropersAndRepacks = DownloadPropersAndRepacks.PREFER_AND_UPGRADE)
	{
		var revisionUpgradeAtSameQuality = profile.GetIndex(current) == profile.GetIndex(candidate)
			&& IsRevisionUpgrade(current.Revision, candidate.Revision, downloadPropersAndRepacks);

		return revisionUpgradeAtSameQuality
			|| profile.IsUpgrade(current, candidate, currentScore, candidateScore, upgradeAllowed, downloadPropersAndRepacks);
	}

	// on the same tier a release can still replace the current file when its custom format score beats the
	// current score and reaches the profile's cutoff format score
	private static bool IsFormatScoreUpgrade(QualityProfile profile, int currentScore, int candidateScore)
		=> profile.CutoffFormatScore != 0
		   && candidateScore > currentScore
		   && candidateScore >= profile.CutoffFormatScore;

	/// <summary>
	///     Whether the candidate revision replaces the current revision: a higher version always, and a proper or repack
	///     only when propers and repacks are preferred.
	/// </summary>
	public static bool IsRevisionUpgrade(Revision current, Revision candidate, DownloadPropersAndRepacks preference)
	{
		if (candidate.Version != current.Version)
		{
			return candidate.Version > current.Version;
		}

		if (preference is DownloadPropersAndRepacks.DO_NOT_UPGRADE or DownloadPropersAndRepacks.DO_NOT_PREFER)
		{
			return false;
		}

		return (candidate.IsProper || candidate.IsRepack) && !current.IsProper && !current.IsRepack;
	}
}
