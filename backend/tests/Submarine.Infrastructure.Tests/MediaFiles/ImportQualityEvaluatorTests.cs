using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.MediaFiles;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Infrastructure.Tests.MediaFiles;

public sealed class ImportQualityEvaluatorTests
{
	private static readonly QualityModel Sd = new(new QualityResolutionModel(QualitySource.TV), new Revision());
	private static readonly QualityModel WebDl720 = new(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P), new Revision());
	private static readonly QualityModel WebDl1080 = new(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision());

	private static QualityProfile ProfileAllowingUpTo1080(bool upgradeAllowed = true)
		=> new()
		{
			UpgradeAllowed = upgradeAllowed,
			Cutoff = 2,
			Items =
			[
				new QualityProfileItem(new QualityResolutionModel(QualitySource.TV), true),
				new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P), true),
				new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)
			]
		};

	private static LanguageProfile EnglishProfile(bool upgradeAllowed = true)
		=> new() { Languages = [Language.ENGLISH, Language.FRENCH], Cutoff = Language.ENGLISH, UpgradeAllowed = upgradeAllowed };

	[Fact]
	public void Evaluate_ShouldAccept_WhenNoExistingFile()
	{
		var decision = ImportQualityEvaluator.Evaluate(
			ProfileAllowingUpTo1080(), EnglishProfile(), WebDl720, [Language.ENGLISH], 0,
			existingQuality: null, existingLanguages: null, existingFormatScore: 0);

		decision.ShouldImport.ShouldBeTrue();
		decision.IsUpgrade.ShouldBeFalse();
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenQualityNotInProfile()
	{
		var profile = ProfileAllowingUpTo1080();
		profile.Items[2] = profile.Items[2] with { Allowed = false };

		var decision = ImportQualityEvaluator.Evaluate(
			profile, EnglishProfile(), WebDl1080, [Language.ENGLISH], 0,
			existingQuality: null, existingLanguages: null, existingFormatScore: 0);

		decision.ShouldImport.ShouldBeFalse();
		decision.Rejection.ShouldBe(ImportRejectionReason.NOT_WANTED_QUALITY);
	}

	[Fact]
	public void Evaluate_ShouldAcceptAsUpgrade_WhenCandidateQualityIsHigherTier()
	{
		var decision = ImportQualityEvaluator.Evaluate(
			ProfileAllowingUpTo1080(), EnglishProfile(), WebDl1080, [Language.ENGLISH], 0,
			existingQuality: WebDl720, existingLanguages: [Language.ENGLISH], existingFormatScore: 0);

		decision.ShouldImport.ShouldBeTrue();
		decision.IsUpgrade.ShouldBeTrue();
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenCandidateQualityIsSame_AndNoLanguageOrFormatUpgrade()
	{
		var decision = ImportQualityEvaluator.Evaluate(
			ProfileAllowingUpTo1080(), EnglishProfile(), WebDl720, [Language.ENGLISH], 0,
			existingQuality: WebDl720, existingLanguages: [Language.ENGLISH], existingFormatScore: 0);

		decision.ShouldImport.ShouldBeFalse();
		decision.Rejection.ShouldBe(ImportRejectionReason.SAME_OR_WORSE_QUALITY);
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenCandidateQualityIsLowerTier()
	{
		var decision = ImportQualityEvaluator.Evaluate(
			ProfileAllowingUpTo1080(), EnglishProfile(), Sd, [Language.ENGLISH], 0,
			existingQuality: WebDl720, existingLanguages: [Language.ENGLISH], existingFormatScore: 0);

		decision.ShouldImport.ShouldBeFalse();
		decision.Rejection.ShouldBe(ImportRejectionReason.SAME_OR_WORSE_QUALITY);
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenUpgradeNotAllowed_EvenIfQualityIsHigher()
	{
		var decision = ImportQualityEvaluator.Evaluate(
			ProfileAllowingUpTo1080(upgradeAllowed: false), EnglishProfile(), WebDl1080, [Language.ENGLISH], 0,
			existingQuality: WebDl720, existingLanguages: [Language.ENGLISH], existingFormatScore: 0);

		decision.ShouldImport.ShouldBeFalse();
	}

	[Fact]
	public void Evaluate_ShouldAcceptAsUpgrade_WhenSameQualityButLanguageRanksBetter()
	{
		var decision = ImportQualityEvaluator.Evaluate(
			ProfileAllowingUpTo1080(), EnglishProfile(), WebDl720, [Language.ENGLISH], 0,
			existingQuality: WebDl720, existingLanguages: [Language.FRENCH], existingFormatScore: 0);

		decision.ShouldImport.ShouldBeTrue();
		decision.IsUpgrade.ShouldBeTrue();
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenSameQualityAndLanguageUpgradeDisabled()
	{
		var decision = ImportQualityEvaluator.Evaluate(
			ProfileAllowingUpTo1080(), EnglishProfile(upgradeAllowed: false), WebDl720, [Language.ENGLISH], 0,
			existingQuality: WebDl720, existingLanguages: [Language.FRENCH], existingFormatScore: 0);

		decision.ShouldImport.ShouldBeFalse();
	}

	[Fact]
	public void Evaluate_ShouldAcceptAsUpgrade_WhenSameQualityButFormatScoreClearsCutoff()
	{
		var profile = ProfileAllowingUpTo1080();
		profile.CutoffFormatScore = 10;

		var decision = ImportQualityEvaluator.Evaluate(
			profile, EnglishProfile(), WebDl720, [Language.ENGLISH], candidateFormatScore: 15,
			existingQuality: WebDl720, existingLanguages: [Language.ENGLISH], existingFormatScore: 0);

		decision.ShouldImport.ShouldBeTrue();
		decision.IsUpgrade.ShouldBeTrue();
	}
}
