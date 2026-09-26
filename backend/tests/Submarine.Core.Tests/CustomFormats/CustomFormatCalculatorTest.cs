using System.Text.Json;
using Submarine.Core.CustomFormats;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.CustomFormats;

public class CustomFormatCalculatorTest
{
	[Fact]
	public void Match_ShouldMatchFormat_WhenAllRequiredSpecificationsMatch()
	{
		var format = CreateFormat(1,
			Spec(CustomFormatSpecificationType.QUALITY_SOURCE, "WEB_DL"),
			Spec(CustomFormatSpecificationType.RESOLUTION, "R1080_P"));

		var result = CustomFormatCalculator.Match([format], CreateContext());

		result.ShouldBe([format]);
	}

	[Fact]
	public void Match_ShouldNotMatchFormat_WhenARequiredSpecificationFails()
	{
		var format = CreateFormat(1,
			Spec(CustomFormatSpecificationType.QUALITY_SOURCE, "WEB_DL"),
			Spec(CustomFormatSpecificationType.RESOLUTION, "R2160_P"));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldBeEmpty();
	}

	[Fact]
	public void Match_ShouldMatchFormat_WhenRequiredAndAtLeastOneOptionalSpecificationMatch()
	{
		var format = CreateFormat(1,
			Spec(CustomFormatSpecificationType.QUALITY_SOURCE, "WEB_DL"),
			Spec(CustomFormatSpecificationType.STREAMING_PROVIDER, "NETFLIX", required: false),
			Spec(CustomFormatSpecificationType.STREAMING_PROVIDER, "AMAZON", required: false));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldHaveSingleItem();
	}

	[Fact]
	public void Match_ShouldNotMatchFormat_WhenRequiredMatchesButNoOptionalSpecificationMatches()
	{
		var format = CreateFormat(1,
			Spec(CustomFormatSpecificationType.QUALITY_SOURCE, "WEB_DL"),
			Spec(CustomFormatSpecificationType.STREAMING_PROVIDER, "NETFLIX", required: false),
			Spec(CustomFormatSpecificationType.RESOLUTION, "R2160_P", required: false));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldBeEmpty();
	}

	[Fact]
	public void Match_ShouldRequireAMatchInEveryOptionalGroup()
	{
		// one optional group (language) matches, the other (protocol) does not, so the format does not match
		var format = CreateFormat(1,
			Spec(CustomFormatSpecificationType.LANGUAGE, "ENGLISH", required: false),
			Spec(CustomFormatSpecificationType.PROTOCOL, "USENET", required: false));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("EVO", false)]
	[InlineData("FLUX", true)]
	public void Match_ShouldInvertResult_WhenSpecificationIsNegated(string group, bool expected)
	{
		var format = CreateFormat(1,
			Spec(CustomFormatSpecificationType.RELEASE_GROUP, $"^{group}$", negated: true));

		CustomFormatCalculator.Match([format], CreateContext()).Count.ShouldBe(expected ? 1 : 0);
	}

	[Theory]
	[InlineData("Movie.Title.2021.2160p.WEB-DL.DDP5.1.HDR.HEVC-FLUX", @"\bHDR(10)?\b")]
	[InlineData("Series.Title.S01E01.1080p.WEB-DL.x265-GROUP", @"\b(x|h)\.?265\b")]
	public void Match_ShouldMatchFormat_WhenReleaseTitleMatchesRegex(string title, string pattern)
	{
		var context = CreateContext() with { Title = title };
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.RELEASE_TITLE, pattern));

		CustomFormatCalculator.Match([format], context).ShouldHaveSingleItem();
	}

	[Theory]
	[InlineData("GERMAN")]
	[InlineData("german")]
	public void Match_ShouldMatchFormat_WhenLanguageMatchesExactly(string value)
	{
		var release = CreateRelease() with { Languages = [Language.GERMAN, Language.ENGLISH] };
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.LANGUAGE, value));

		CustomFormatCalculator.Match([format], CreateContext(release)).ShouldHaveSingleItem();
	}

	[Fact]
	public void Match_ShouldMatchFormat_WhenStreamingProviderMatches()
	{
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.STREAMING_PROVIDER, "AMAZON"));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldHaveSingleItem();
	}

	[Theory]
	[InlineData("Directors Cut", @"director'?s[ ._-]cut")]
	[InlineData("IMAX Enhanced", @"\bIMAX\b")]
	public void Match_ShouldMatchFormat_WhenEditionMatchesRegex(string edition, string pattern)
	{
		var release = CreateRelease() with { MovieReleaseData = new MovieReleaseData { Edition = edition } };
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.EDITION, pattern));

		CustomFormatCalculator.Match([format], CreateContext(release)).ShouldHaveSingleItem();
	}

	[Fact]
	public void Match_ShouldMatchFormat_WhenTorrentReleaseHasFlag()
	{
		var release = CreateRelease().ToTorrent() with
		{
			Flags = TorrentReleaseFlags.FREELEECH | TorrentReleaseFlags.INTERNAL
		};
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.RELEASE_FLAG, "FREELEECH"));

		CustomFormatCalculator.Match([format], CreateContext(release)).ShouldHaveSingleItem();
	}

	[Fact]
	public void Match_ShouldNotMatchReleaseFlagSpecification_WhenReleaseIsNotTorrent()
	{
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.RELEASE_FLAG, "FREELEECH"));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldBeEmpty();
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void Match_ShouldMatchHardcodedSubsSpecification_WhenReleaseHasHardcodedSubs(bool hardcodedSubs, bool expected)
	{
		var release = CreateRelease() with { HardcodedSubs = hardcodedSubs };
		var format = CreateFormat(1, new CustomFormatSpecification(
			"Hardcoded", CustomFormatSpecificationType.HARDCODED_SUBS, false, true, JsonSerializer.SerializeToElement(true)));

		CustomFormatCalculator.Match([format], CreateContext(release)).Count.ShouldBe(expected ? 1 : 0);
	}

	[Fact]
	public void Match_ShouldNotMatchFormat_WhenEnumValueIsUnparsable()
	{
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.LANGUAGE, "KLINGON"));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldBeEmpty();
	}

	[Fact]
	public void Match_ShouldReturnOnlyMatchingFormats_WhenMultipleFormatsAreGiven()
	{
		var matching = CreateFormat(1, Spec(CustomFormatSpecificationType.RESOLUTION, "R1080_P"));
		var nonMatching = CreateFormat(2, Spec(CustomFormatSpecificationType.PROTOCOL, "USENET"));

		var result = CustomFormatCalculator.Match([matching, nonMatching], CreateContext());

		result.ShouldBe([matching]);
	}

	[Fact]
	public void Match_ShouldMatchSize_WhenSizeIsInsideTheRange()
	{
		var format = CreateFormat(1, RangeSpec(CustomFormatSpecificationType.SIZE, min: 1, max: 10));

		CustomFormatCalculator.Match([format], CreateContext().Size(2 * 1024L * 1024 * 1024)).ShouldHaveSingleItem();
		CustomFormatCalculator.Match([format], CreateContext().Size(20L * 1024 * 1024 * 1024)).ShouldBeEmpty();
		CustomFormatCalculator.Match([format], CreateContext()).ShouldBeEmpty();
	}

	[Fact]
	public void Match_ShouldMatchSize_WhenOnlyOneBoundIsSet()
	{
		var format = CreateFormat(1, RangeSpec(CustomFormatSpecificationType.SIZE, min: 5, max: null));

		CustomFormatCalculator.Match([format], CreateContext().Size(10L * 1024 * 1024 * 1024)).ShouldHaveSingleItem();
		CustomFormatCalculator.Match([format], CreateContext().Size(1L * 1024 * 1024 * 1024)).ShouldBeEmpty();
	}

	[Fact]
	public void Match_ShouldMatchYear_WhenYearIsInsideTheRange()
	{
		var format = CreateFormat(1, RangeSpec(CustomFormatSpecificationType.YEAR, min: 2000, max: 2010));
		var release = CreateRelease() with { Year = 2005 };

		CustomFormatCalculator.Match([format], CreateContext(release)).ShouldHaveSingleItem();
		CustomFormatCalculator.Match([format], CreateContext(CreateRelease() with { Year = 2020 })).ShouldBeEmpty();
	}

	[Fact]
	public void Match_ShouldMatchIndexerFlag_WhenContextCarriesIt()
	{
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.INDEXER_FLAG, "FREELEECH"));

		var context = CreateContext() with { IndexerFlags = [IndexerFlag.FREELEECH] };
		CustomFormatCalculator.Match([format], context).ShouldHaveSingleItem();

		var withoutFlag = CreateContext() with { IndexerFlags = [IndexerFlag.INTERNAL] };
		CustomFormatCalculator.Match([format], withoutFlag).ShouldBeEmpty();
	}

	[Fact]
	public void Match_ShouldMatchProtocol_WhenProtocolMatches()
	{
		var format = CreateFormat(1, Spec(CustomFormatSpecificationType.PROTOCOL, "BITTORRENT"));

		CustomFormatCalculator.Match([format], CreateContext()).ShouldHaveSingleItem();
	}

	[Fact]
	public void Score_ShouldSumTheProfileScoresOfMatchedFormats()
	{
		var profile = new QualityProfile
		{
			Name = "Test",
			FormatItems = [new ProfileFormatItem(1, 100), new ProfileFormatItem(2, -25)]
		};
		var good = CreateFormat(1);
		var bad = CreateFormat(2);
		var unscored = CreateFormat(3);

		CustomFormatCalculator.Score(profile, [good, bad, unscored]).ShouldBe(75);
		CustomFormatCalculator.Score(profile, []).ShouldBe(0);
	}

	private static ReleaseContext CreateContext(BaseRelease? release = null)
	{
		var parsed = release ?? CreateRelease();

		return new ReleaseContext(parsed.FullTitle, parsed, Protocol: parsed.Protocol, Indexer: "MyIndexer");
	}

	private static BaseRelease CreateRelease()
		=> new()
		{
			FullTitle = "Movie.Title.2021.1080p.AMZN.WEB-DL.DDP5.1.H.264-EVO",
			Title = "Movie Title",
			Year = 2021,
			Languages = [Language.ENGLISH],
			StreamingProvider = StreamingProvider.AMAZON,
			Type = ReleaseType.MOVIE,
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision()),
			Protocol = Protocol.BITTORRENT,
			ReleaseGroup = "EVO"
		};

	private static CustomFormat CreateFormat(int id, params CustomFormatSpecification[] specifications)
		=> new()
		{
			Id = id,
			Name = $"Format {id}",
			Specifications = [.. specifications]
		};

	private static CustomFormatSpecification Spec(
		CustomFormatSpecificationType type,
		string value,
		bool required = true,
		bool negated = false)
		=> new(value, type, negated, required, Json(value));

	private static CustomFormatSpecification RangeSpec(
		CustomFormatSpecificationType type,
		double? min,
		double? max)
		=> new($"{type}", type, false, true, JsonSerializer.SerializeToElement(new { min, max }));

	private static JsonElement Json(string value)
		=> JsonSerializer.SerializeToElement(value);
}

internal static class ReleaseContextTestExtensions
{
	public static ReleaseContext Size(this ReleaseContext context, long size)
		=> context with { Size = size };
}
