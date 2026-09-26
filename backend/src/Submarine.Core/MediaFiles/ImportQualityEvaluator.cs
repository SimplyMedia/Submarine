using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;

namespace Submarine.Core.MediaFiles;

/// <summary>
///     Why a candidate file was rejected during import.
/// </summary>
public enum ImportRejectionReason
{
	/// <summary>The quality is not allowed by the profile.</summary>
	NOT_WANTED_QUALITY,

	/// <summary>An existing file already holds an equal or better quality and language.</summary>
	SAME_OR_WORSE_QUALITY,

	/// <summary>The file is a sample.</summary>
	SAMPLE,

	/// <summary>The file is an in-progress download artifact.</summary>
	INCOMPLETE,

	/// <summary>No episode or movie could be matched for the file.</summary>
	NO_MEDIA_MATCH,

	/// <summary>The file could not be read or probed.</summary>
	UNREADABLE
}

/// <summary>
///     Whether a candidate file should be imported, replacing any existing file.
/// </summary>
/// <param name="ShouldImport">Whether the file should be imported.</param>
/// <param name="IsUpgrade">Whether the import replaces an existing file.</param>
/// <param name="Rejection">Why the file was rejected, when <see cref="ShouldImport" /> is false.</param>
public sealed record ImportQualityDecision(bool ShouldImport, bool IsUpgrade, ImportRejectionReason? Rejection);

/// <summary>
///     Decides whether a candidate media file should be imported over an existing one, reusing the
///     quality and language profile upgrade rules the decision engine uses for grabs.
/// </summary>
public static class ImportQualityEvaluator
{
	/// <summary>
	///     Evaluate a candidate file against the profile and, when one exists, the currently held file.
	/// </summary>
	public static ImportQualityDecision Evaluate(
		QualityProfile qualityProfile,
		LanguageProfile languageProfile,
		QualityModel candidateQuality,
		IReadOnlyList<Language> candidateLanguages,
		int candidateFormatScore,
		QualityModel? existingQuality,
		IReadOnlyList<Language>? existingLanguages,
		int existingFormatScore,
		DownloadPropersAndRepacks downloadPropersAndRepacks = DownloadPropersAndRepacks.PREFER_AND_UPGRADE)
	{
		if (!qualityProfile.IsAllowed(candidateQuality))
		{
			return new ImportQualityDecision(false, false, ImportRejectionReason.NOT_WANTED_QUALITY);
		}

		if (existingQuality is null)
		{
			return new ImportQualityDecision(true, false, null);
		}

		var qualityUpgrade = qualityProfile.IsQualityUpgrade(
			existingQuality,
			candidateQuality,
			existingFormatScore,
			candidateFormatScore,
			qualityProfile.UpgradeAllowed,
			downloadPropersAndRepacks);

		var qualityNotWorse = qualityProfile.GetIndex(candidateQuality) >= qualityProfile.GetIndex(existingQuality);

		// an upgrade in either dimension is enough, but a quality downgrade is never traded for a language gain
		var languageUpgrade = languageProfile.IsUpgrade(existingLanguages ?? [], candidateLanguages) && qualityNotWorse;

		return qualityUpgrade || languageUpgrade
			? new ImportQualityDecision(true, true, null)
			: new ImportQualityDecision(false, false, ImportRejectionReason.SAME_OR_WORSE_QUALITY);
	}
}
