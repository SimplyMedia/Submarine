using System.Text.Json;
using Shouldly;
using Submarine.Core.AutoTagging;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Core.Tests.AutoTagging;

/// <summary>
///     Auto tagging rule specification matching.
/// </summary>
public sealed class AutoTaggingCalculatorTests
{
	private static readonly AutoTaggingContext SeriesContext = new(
		["Drama", "Comedy"],
		[1, 2],
		[10],
		SeriesType.ANIME,
		"CONTINUING",
		2020,
		true,
		"Test Network");

	private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

	[Fact]
	public void Match_ShouldMatch_WhenGenreSpecificationMatches()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Genre", AutoTaggingSpecificationType.GENRE, Negate: false, Required: true, Json("""["drama"]"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldNotMatch_WhenRequiredGenreMissing()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Genre", AutoTaggingSpecificationType.GENRE, Negate: false, Required: true, Json("""["horror"]"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeFalse();
	}

	[Fact]
	public void Match_ShouldNegate_WhenNegateSet()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Genre", AutoTaggingSpecificationType.GENRE, Negate: true, Required: true, Json("""["horror"]"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldMatch_WhenYearWithinRange()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Year", AutoTaggingSpecificationType.YEAR, Negate: false, Required: true, Json("""{"min":2018,"max":2022}"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldNotMatch_WhenYearOutsideRange()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Year", AutoTaggingSpecificationType.YEAR, Negate: false, Required: true, Json("""{"min":2021,"max":2022}"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeFalse();
	}

	[Fact]
	public void Match_ShouldMatch_WhenRootFolderUsedByAnyVersion()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Root", AutoTaggingSpecificationType.ROOT_FOLDER, Negate: false, Required: true, Json("2"))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldMatch_WhenQualityProfileUsedByAnyVersion()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Quality", AutoTaggingSpecificationType.QUALITY_PROFILE, Negate: false, Required: true, Json("10"))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldMatch_WhenSeriesTypeMatches()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Type", AutoTaggingSpecificationType.SERIES_TYPE, Negate: false, Required: true, Json("\"anime\""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldNotMatch_WhenSeriesTypeSpecificationAppliedToMovieContext()
	{
		var movieContext = SeriesContext with { SeriesType = null };
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Type", AutoTaggingSpecificationType.SERIES_TYPE, Negate: false, Required: true, Json("\"anime\""))]
		};

		AutoTaggingCalculator.Match(rule, movieContext).ShouldBeFalse();
	}

	[Fact]
	public void Match_ShouldMatch_WhenStatusMatches()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Status", AutoTaggingSpecificationType.STATUS, Negate: false, Required: true, Json("\"continuing\""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldMatch_WhenMonitored()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Monitored", AutoTaggingSpecificationType.MONITORED, Negate: false, Required: true, null)]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
		AutoTaggingCalculator.Match(rule, SeriesContext with { Monitored = false }).ShouldBeFalse();
	}

	[Fact]
	public void Match_ShouldMatch_WhenNetworkOrStudioMatches()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Network", AutoTaggingSpecificationType.NETWORK_OR_STUDIO, Negate: false, Required: true, Json("""["test network"]"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldRequireAllRequiredSpecifications()
	{
		var rule = new AutoTaggingRule
		{
			Specifications =
			[
				new AutoTaggingSpecification("Genre", AutoTaggingSpecificationType.GENRE, Negate: false, Required: true, Json("""["drama"]""")),
				new AutoTaggingSpecification("Year", AutoTaggingSpecificationType.YEAR, Negate: false, Required: true, Json("""{"min":2021,"max":2022}"""))
			]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeFalse();
	}

	[Fact]
	public void Match_ShouldMatch_WhenAtLeastOneNonRequiredSpecificationInGroupMatches()
	{
		var rule = new AutoTaggingRule
		{
			Specifications =
			[
				new AutoTaggingSpecification("Genre horror", AutoTaggingSpecificationType.GENRE, Negate: false, Required: false, Json("""["horror"]""")),
				new AutoTaggingSpecification("Genre drama", AutoTaggingSpecificationType.GENRE, Negate: false, Required: false, Json("""["drama"]"""))
			]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldNotMatch_WhenNoNonRequiredSpecificationInGroupMatches()
	{
		var rule = new AutoTaggingRule
		{
			Specifications =
			[
				new AutoTaggingSpecification("Genre horror", AutoTaggingSpecificationType.GENRE, Negate: false, Required: false, Json("""["horror"]""")),
				new AutoTaggingSpecification("Genre scifi", AutoTaggingSpecificationType.GENRE, Negate: false, Required: false, Json("""["sci-fi"]"""))
			]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext).ShouldBeFalse();
	}

	[Fact]
	public void Match_ShouldMatchOriginalLanguageCaseInsensitively()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Original language", AutoTaggingSpecificationType.ORIGINAL_LANGUAGE, Negate: false, Required: true, Json("""["en"]"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext with { OriginalLanguage = "EN" }).ShouldBeTrue();
	}

	[Fact]
	public void Match_ShouldMatchAnyMovieKeywordCaseInsensitively()
	{
		var rule = new AutoTaggingRule
		{
			Specifications = [new AutoTaggingSpecification("Keyword", AutoTaggingSpecificationType.KEYWORD, Negate: false, Required: true, Json("""["heist"]"""))]
		};

		AutoTaggingCalculator.Match(rule, SeriesContext with { Keywords = ["Dreams", "Heist"] }).ShouldBeTrue();
		AutoTaggingCalculator.Match(rule, SeriesContext with { Keywords = ["Dreams"] }).ShouldBeFalse();
	}
}
