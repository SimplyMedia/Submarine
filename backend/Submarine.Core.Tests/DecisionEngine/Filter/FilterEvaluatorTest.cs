using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Tests.DecisionEngine.Filter;

public class FilterEvaluatorTest
{
	private readonly FilterEvaluator _instance = new();

	[Fact]
	public void Evaluate_ShouldNotReject_WhenAllowFilterMatches()
	{
		var result = _instance.Evaluate(Context(),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.ALLOW, new[] { "FLUX" }) });

		Assert.Empty(result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenNoAllowFilterMatchesOnField()
	{
		var result = _instance.Evaluate(Context(releaseGroup: "EVO"),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.ALLOW, new[] { "FLUX" }) });

		Assert.Contains("release group not allowed", result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenBlockFilterMatches()
	{
		var result = _instance.Evaluate(Context(),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.BLOCK, new[] { "FLUX" }) });

		Assert.Contains("blocked by release group filter", result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldNotReject_WhenBlockFilterDoesNotMatch()
	{
		var result = _instance.Evaluate(Context(),
			new[] { Filter(FilterField.INDEXER, FilterMode.BLOCK, new[] { "OtherIndexer" }) });

		Assert.Empty(result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenIndexerBlockMatches()
	{
		var result = _instance.Evaluate(Context(),
			new[] { Filter(FilterField.INDEXER, FilterMode.BLOCK, new[] { "MyIndexer" }) });

		Assert.Contains("blocked by indexer filter", result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldReject_WhenQualityAllowMisses()
	{
		var result = _instance.Evaluate(Context(quality: "HDTV-720p"),
			new[] { Filter(FilterField.QUALITY, FilterMode.ALLOW, new[] { "WebDL-1080p" }) });

		Assert.Contains("quality not allowed", result.Rejections);
	}

	[Theory]
	[InlineData(1, 900)]
	[InlineData(2, 800)]
	public void Evaluate_ShouldScoreByTier_WhenPreferFilterMatches(int tier, int expected)
	{
		var result = _instance.Evaluate(Context(),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.PREFER, new[] { "FLUX" }, tier) });

		Assert.Equal(expected, result.Score);
	}

	[Fact]
	public void Evaluate_ShouldUseBestTier_WhenMultiplePreferFiltersMatchSameField()
	{
		var result = _instance.Evaluate(Context(),
			new[]
			{
				Filter(FilterField.RELEASE_GROUP, FilterMode.PREFER, new[] { "FLUX" }, 2),
				Filter(FilterField.RELEASE_GROUP, FilterMode.PREFER, new[] { "FLUX" }, 1)
			});

		Assert.Equal(900, result.Score);
	}

	[Fact]
	public void Evaluate_ShouldAccumulateScore_WhenPreferFiltersMatchMultipleFields()
	{
		var result = _instance.Evaluate(Context(),
			new[]
			{
				Filter(FilterField.RELEASE_GROUP, FilterMode.PREFER, new[] { "FLUX" }, 1),
				Filter(FilterField.SOURCE, FilterMode.PREFER, new[] { "AMAZON" }, 2)
			});

		Assert.Equal(1700, result.Score);
	}

	[Fact]
	public void Evaluate_ShouldMatchLanguage_WhenAnyReleaseLanguageMatches()
	{
		var result = _instance.Evaluate(Context(languages: new[] { Language.GERMAN, Language.ENGLISH }),
			new[] { Filter(FilterField.LANGUAGE, FilterMode.ALLOW, new[] { "ENGLISH" }) });

		Assert.Empty(result.Rejections);
	}

	[Theory]
	[InlineData("flux")]
	[InlineData("FLUX")]
	[InlineData("FlUx")]
	public void Evaluate_ShouldMatchCaseInsensitively_WhenValueDiffersInCasing(string value)
	{
		var result = _instance.Evaluate(Context(),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.BLOCK, new[] { value }) });

		Assert.Contains("blocked by release group filter", result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldNotReject_WhenBlockFieldIsNull()
	{
		var result = _instance.Evaluate(Context(releaseGroup: null),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.BLOCK, new[] { "FLUX" }) });

		Assert.Empty(result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldRejectWithMissingReason_WhenAllowFieldIsNull()
	{
		var result = _instance.Evaluate(Context(releaseGroup: null),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.ALLOW, new[] { "FLUX" }) });

		Assert.Contains("no release group", result.Rejections);
	}

	[Fact]
	public void Evaluate_ShouldAddNoScore_WhenPreferFieldIsNull()
	{
		var result = _instance.Evaluate(Context(releaseGroup: null),
			new[] { Filter(FilterField.RELEASE_GROUP, FilterMode.PREFER, new[] { "FLUX" }, 1) });

		Assert.Equal(0, result.Score);
	}

	private static FilterContext Context(
		string? releaseGroup = "FLUX",
		string? indexerName = "MyIndexer",
		string? quality = "WebDL-1080p",
		IReadOnlyList<Language>? languages = null,
		StreamingProvider? source = StreamingProvider.AMAZON)
		=> new(releaseGroup, indexerName, quality, languages ?? new[] { Language.ENGLISH }, source);

	private static ReleaseFilter Filter(FilterField field, FilterMode mode, IReadOnlyList<string> values, int tier = 0)
		=> new(1, field, values, mode, tier);
}
