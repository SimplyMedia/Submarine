using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Xunit;
using Submarine.Core.DecisionEngine.CustomFormats;

namespace Submarine.Core.Tests.DecisionEngine.CustomFormats;

public class CustomFormatEvaluatorTest
{
	private readonly CustomFormatEvaluator _instance;

	public CustomFormatEvaluatorTest(ITestOutputHelper output)
		=> _instance = new CustomFormatEvaluator(new XunitLogger<CustomFormatEvaluator>(output));

	[Fact]
	public void Evaluate_ShouldMatchFormat_WhenAllRequiredConditionsMatch()
	{
		var format = CreateFormat(1,
			Condition(CustomFormatConditionType.QUALITY_SOURCE, "WEB_DL"),
			Condition(CustomFormatConditionType.RESOLUTION, "R1080_P"));

		var result = _instance.Evaluate(CreateRelease(), new[] { format });

		Assert.Equal(new[] { format }, result);
	}

	[Fact]
	public void Evaluate_ShouldNotMatchFormat_WhenARequiredConditionFails()
	{
		var format = CreateFormat(1,
			Condition(CustomFormatConditionType.QUALITY_SOURCE, "WEB_DL"),
			Condition(CustomFormatConditionType.RESOLUTION, "R2160_P"));

		Assert.Empty(_instance.Evaluate(CreateRelease(), new[] { format }));
	}

	[Fact]
	public void Evaluate_ShouldMatchFormat_WhenRequiredAndAtLeastOneOptionalConditionMatch()
	{
		var format = CreateFormat(1,
			Condition(CustomFormatConditionType.QUALITY_SOURCE, "WEB_DL"),
			Condition(CustomFormatConditionType.STREAMING_PROVIDER, "NETFLIX", false),
			Condition(CustomFormatConditionType.STREAMING_PROVIDER, "AMAZON", false));

		Assert.Single(_instance.Evaluate(CreateRelease(), new[] { format }));
	}

	[Fact]
	public void Evaluate_ShouldNotMatchFormat_WhenRequiredMatchesButNoOptionalConditionMatches()
	{
		var format = CreateFormat(1,
			Condition(CustomFormatConditionType.QUALITY_SOURCE, "WEB_DL"),
			Condition(CustomFormatConditionType.STREAMING_PROVIDER, "NETFLIX", false),
			Condition(CustomFormatConditionType.RESOLUTION, "R2160_P", false));

		Assert.Empty(_instance.Evaluate(CreateRelease(), new[] { format }));
	}

	[Theory]
	[InlineData("EVO", false)]
	[InlineData("FLUX", true)]
	public void Evaluate_ShouldInvertConditionResult_WhenConditionIsNegated(string group, bool expected)
	{
		var format = CreateFormat(1,
			Condition(CustomFormatConditionType.RELEASE_GROUP, $"^{group}$", negated: true));

		var result = _instance.Evaluate(CreateRelease(), new[] { format });

		Assert.Equal(expected, result.Count == 1);
	}

	[Theory]
	[InlineData("Movie.Title.2021.2160p.WEB-DL.DDP5.1.HDR.HEVC-FLUX", @"\bHDR(10)?\b")]
	[InlineData("Series.Title.S01E01.1080p.WEB-DL.x265-GROUP", @"\b(x|h)\.?265\b")]
	public void Evaluate_ShouldMatchFormat_WhenReleaseTitleMatchesRegex(string title, string pattern)
	{
		var release = CreateRelease() with { FullTitle = title };
		var format = CreateFormat(1, Condition(CustomFormatConditionType.RELEASE_TITLE, pattern));

		Assert.Single(_instance.Evaluate(release, new[] { format }));
	}

	[Theory]
	[InlineData("GERMAN")]
	[InlineData("german")]
	public void Evaluate_ShouldMatchFormat_WhenLanguageMatchesExactly(string value)
	{
		var release = CreateRelease() with { Languages = new[] { Language.GERMAN, Language.ENGLISH } };
		var format = CreateFormat(1, Condition(CustomFormatConditionType.LANGUAGE, value));

		Assert.Single(_instance.Evaluate(release, new[] { format }));
	}

	[Fact]
	public void Evaluate_ShouldMatchFormat_WhenStreamingProviderMatches()
	{
		var format = CreateFormat(1, Condition(CustomFormatConditionType.STREAMING_PROVIDER, "AMAZON"));

		Assert.Single(_instance.Evaluate(CreateRelease(), new[] { format }));
	}

	[Theory]
	[InlineData("Directors Cut", @"director'?s[ ._-]cut")]
	[InlineData("IMAX Enhanced", @"\bIMAX\b")]
	public void Evaluate_ShouldMatchFormat_WhenEditionMatchesRegex(string edition, string pattern)
	{
		var release = CreateRelease() with { MovieReleaseData = new MovieReleaseData { Edition = edition } };
		var format = CreateFormat(1, Condition(CustomFormatConditionType.EDITION, pattern));

		Assert.Single(_instance.Evaluate(release, new[] { format }));
	}

	[Fact]
	public void Evaluate_ShouldMatchFormat_WhenTorrentReleaseHasFlag()
	{
		var release = CreateRelease().ToTorrent() with
		{
			Flags = TorrentReleaseFlags.FREELEECH | TorrentReleaseFlags.INTERNAL
		};
		var format = CreateFormat(1, Condition(CustomFormatConditionType.RELEASE_FLAG, "FREELEECH"));

		Assert.Single(_instance.Evaluate(release, new[] { format }));
	}

	[Fact]
	public void Evaluate_ShouldNotMatchReleaseFlagCondition_WhenReleaseIsNotTorrent()
	{
		var format = CreateFormat(1, Condition(CustomFormatConditionType.RELEASE_FLAG, "FREELEECH"));

		Assert.Empty(_instance.Evaluate(CreateRelease(), new[] { format }));
	}

	[Fact]
	public void Evaluate_ShouldNotMatchFormat_WhenEnumValueIsUnparsable()
	{
		var format = CreateFormat(1, Condition(CustomFormatConditionType.LANGUAGE, "KLINGON"));

		Assert.Empty(_instance.Evaluate(CreateRelease(), new[] { format }));
	}

	[Fact]
	public void Evaluate_ShouldReturnOnlyMatchingFormats_WhenMultipleFormatsAreGiven()
	{
		var matching = CreateFormat(1, Condition(CustomFormatConditionType.RESOLUTION, "R1080_P"));
		var nonMatching = CreateFormat(2, Condition(CustomFormatConditionType.PROTOCOL, "USENET"));

		var result = _instance.Evaluate(CreateRelease(), new[] { matching, nonMatching });

		Assert.Equal(new[] { matching }, result);
	}

	private static BaseRelease CreateRelease()
		=> new()
		{
			FullTitle = "Movie.Title.2021.1080p.AMZN.WEB-DL.DDP5.1.H.264-EVO",
			Title = "Movie Title",
			Aliases = Array.Empty<string>(),
			Languages = new[] { Language.ENGLISH },
			StreamingProvider = StreamingProvider.AMAZON,
			Type = ReleaseType.MOVIE,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision()),
			Protocol = Protocol.BITTORRENT,
			ReleaseGroup = "EVO"
		};

	private static CustomFormat CreateFormat(int id, params CustomFormatCondition[] conditions)
		=> new(id, $"Format {id}", conditions);

	private static CustomFormatCondition Condition(CustomFormatConditionType type, string value, bool required = true,
		bool negated = false)
		=> new(value, type, value, required, negated);
}
