using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.DecisionEngine;

public class ReleaseFilterEvaluatorTest
{
	private readonly ReleaseFilterEvaluator _instance = new();

	[Fact]
	public void Evaluate_ShouldNotReject_WhenAllowFilterMatches()
	{
		var result = _instance.Evaluate(Context(),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.ALLOW, ["FLUX"])]);

		result.Rejections.ShouldBeEmpty();
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenNoAllowFilterMatchesOnField()
	{
		var result = _instance.Evaluate(Context(releaseGroup: "EVO"),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.ALLOW, ["FLUX"])]);

		result.Rejections.ShouldContain("release group not allowed");
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenBlockFilterMatches()
	{
		var result = _instance.Evaluate(Context(),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.BLOCK, ["FLUX"])]);

		result.Rejections.ShouldContain("blocked by release group filter");
	}

	[Fact]
	public void Evaluate_ShouldNotReject_WhenBlockFilterDoesNotMatch()
	{
		var result = _instance.Evaluate(Context(),
			[Filter(ReleaseFilterField.INDEXER, ReleaseFilterMode.BLOCK, ["OtherIndexer"])]);

		result.Rejections.ShouldBeEmpty();
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenIndexerBlockMatches()
	{
		var result = _instance.Evaluate(Context(),
			[Filter(ReleaseFilterField.INDEXER, ReleaseFilterMode.BLOCK, ["MyIndexer"])]);

		result.Rejections.ShouldContain("blocked by indexer filter");
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenQualityAllowMisses()
	{
		var result = _instance.Evaluate(Context(quality: "HDTV-720p"),
			[Filter(ReleaseFilterField.QUALITY, ReleaseFilterMode.ALLOW, ["WebDL-1080p"])]);

		result.Rejections.ShouldContain("quality not allowed");
	}

	[Theory]
	[InlineData(1, 900)]
	[InlineData(2, 800)]
	public void Evaluate_ShouldScoreByTier_WhenPreferFilterMatches(int tier, int expected)
	{
		var result = _instance.Evaluate(Context(),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.PREFER, ["FLUX"], tier)]);

		result.Score.ShouldBe(expected);
	}

	[Fact]
	public void Evaluate_ShouldUseBestTier_WhenMultiplePreferFiltersMatchSameField()
	{
		var result = _instance.Evaluate(Context(),
		[
			Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.PREFER, ["FLUX"], 2),
			Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.PREFER, ["FLUX"], 1)
		]);

		result.Score.ShouldBe(900);
	}

	[Fact]
	public void Evaluate_ShouldAccumulateScore_WhenPreferFiltersMatchMultipleFields()
	{
		var result = _instance.Evaluate(Context(),
		[
			Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.PREFER, ["FLUX"], 1),
			Filter(ReleaseFilterField.SOURCE, ReleaseFilterMode.PREFER, ["WEB_DL"], 2)
		]);

		result.Score.ShouldBe(1700);
	}

	[Fact]
	public void Evaluate_ShouldMatchLanguage_WhenAnyReleaseLanguageMatches()
	{
		var result = _instance.Evaluate(Context(languages: [Language.GERMAN, Language.ENGLISH]),
			[Filter(ReleaseFilterField.LANGUAGE, ReleaseFilterMode.ALLOW, ["ENGLISH"])]);

		result.Rejections.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("flux")]
	[InlineData("FLUX")]
	[InlineData("FlUx")]
	public void Evaluate_ShouldMatchCaseInsensitively_WhenValueDiffersInCasing(string value)
	{
		var result = _instance.Evaluate(Context(),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.BLOCK, [value])]);

		result.Rejections.ShouldContain("blocked by release group filter");
	}

	[Fact]
	public void Evaluate_ShouldNotReject_WhenBlockFieldIsNull()
	{
		var result = _instance.Evaluate(Context(releaseGroup: null),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.BLOCK, ["FLUX"])]);

		result.Rejections.ShouldBeEmpty();
	}

	[Fact]
	public void Evaluate_ShouldRejectWithMissingReason_WhenAllowFieldIsNull()
	{
		var result = _instance.Evaluate(Context(releaseGroup: null),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.ALLOW, ["FLUX"])]);

		result.Rejections.ShouldContain("no release group");
	}

	[Fact]
	public void Evaluate_ShouldAddNoScore_WhenPreferFieldIsNull()
	{
		var result = _instance.Evaluate(Context(releaseGroup: null),
			[Filter(ReleaseFilterField.RELEASE_GROUP, ReleaseFilterMode.PREFER, ["FLUX"], 1)]);

		result.Score.ShouldBe(0);
	}

	private static ReleaseCandidate Context(
		string? releaseGroup = "FLUX",
		string indexerName = "MyIndexer",
		string? quality = "WebDL-1080p",
		IReadOnlyList<Language>? languages = null,
		QualitySource? source = QualitySource.WEB_DL)
	{
		var resolution = quality == "HDTV-720p"
			? new QualityResolutionModel(QualitySource.TV, QualityResolution.R720_P)
			: new QualityResolutionModel(source, QualityResolution.R1080_P);

		var release = new BaseRelease
		{
			FullTitle = "Series.Title.S01.1080p.WEB-DL-FLUX",
			Title = "Series Title",
			Quality = new QualityModel(resolution, new Revision()),
			ReleaseGroup = releaseGroup,
			Languages = languages ?? [Language.ENGLISH],
			Protocol = Protocol.BITTORRENT
		};

		return new ReleaseCandidate(release, new ReleaseInfo { Indexer = indexerName });
	}

	private static ReleaseFilter Filter(
		ReleaseFilterField field,
		ReleaseFilterMode mode,
		IReadOnlyList<string> values,
		int tier = 0)
		=> new()
		{
			Id = 1,
			Field = field,
			Values = [.. values],
			Mode = mode,
			Tier = tier
		};
}
