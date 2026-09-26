using System.Text.Json;
using Submarine.Core.CustomFormats;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Quality;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.CustomFormats;

public class TrashCustomFormatJsonTest
{
	private static readonly JsonSerializerOptions TrashOptions = new() { PropertyNameCaseInsensitive = true };

	private const string TrashJson =
		"""
		{
			"name": "BR-DISK",
			"includeCustomFormatWhenRenaming": false,
			"specifications": [
				{
					"name": "BR-DISK",
					"implementation": "ReleaseTitleSpecification",
					"negate": false,
					"required": true,
					"fields": { "value": "\\b(BD(?-i)?)?\\b" }
				},
				{
					"name": "BluRay",
					"implementation": "SourceSpecification",
					"negate": true,
					"required": false,
					"fields": { "value": "BLURAY" }
				}
			]
		}
		""";

	[Fact]
	public void FromTrash_ShouldMapSpecifications_FromTrashJson()
	{
		var format = TrashCustomFormatJson.FromTrash(JsonSerializer.Deserialize<TrashFormat>(TrashJson, TrashOptions)!);

		format.Name.ShouldBe("BR-DISK");
		format.IncludeCustomFormatWhenRenaming.ShouldBeFalse();
		format.Specifications.Count.ShouldBe(2);

		var title = format.Specifications[0];
		title.Type.ShouldBe(CustomFormatSpecificationType.RELEASE_TITLE);
		title.Required.ShouldBeTrue();
		title.Negate.ShouldBeFalse();
		title.Value.ShouldNotBeNull();
		title.Value!.Value.GetString().ShouldBe("\\b(BD(?-i)?)?\\b");

		var source = format.Specifications[1];
		source.Type.ShouldBe(CustomFormatSpecificationType.QUALITY_SOURCE);
		source.Negate.ShouldBeTrue();
		source.Required.ShouldBeFalse();
	}

	[Theory]
	[InlineData("LanguageSpecification", CustomFormatSpecificationType.LANGUAGE)]
	[InlineData("SourceSpecification", CustomFormatSpecificationType.QUALITY_SOURCE)]
	[InlineData("ResolutionSpecification", CustomFormatSpecificationType.RESOLUTION)]
	[InlineData("EditionSpecification", CustomFormatSpecificationType.EDITION)]
	[InlineData("IndexerFlagSpecification", CustomFormatSpecificationType.INDEXER_FLAG)]
	[InlineData("SizeSpecification", CustomFormatSpecificationType.SIZE)]
	[InlineData("YearSpecification", CustomFormatSpecificationType.YEAR)]
	[InlineData("ProtocolSpecification", CustomFormatSpecificationType.PROTOCOL)]
	[InlineData("ReleaseFlagSpecification", CustomFormatSpecificationType.RELEASE_FLAG)]
	[InlineData("HardcodedSubsSpecification", CustomFormatSpecificationType.HARDCODED_SUBS)]
	[InlineData("StreamingServiceSpecification", CustomFormatSpecificationType.STREAMING_PROVIDER)]
	[InlineData("StreamingProviderSpecification", CustomFormatSpecificationType.STREAMING_PROVIDER)]
	[InlineData("ReleaseGroupSpecification", CustomFormatSpecificationType.RELEASE_GROUP)]
	[InlineData("ReleaseTitleSpecification", CustomFormatSpecificationType.RELEASE_TITLE)]
	[InlineData("ReleaseTypeSpecification", CustomFormatSpecificationType.RELEASE_TYPE)]
	[InlineData("QualityModifierSpecification", CustomFormatSpecificationType.QUALITY_MODIFIER)]
	public void FromTrash_ShouldMapEverySupportedImplementation(string implementation, CustomFormatSpecificationType expected)
	{
		var format = new TrashFormat("Test", false,
		[
			new TrashSpecification("Spec", implementation, false, true, Fields("value", Json("WEB_DL")))
		]);

		TrashCustomFormatJson.FromTrash(format).Specifications[0].Type.ShouldBe(expected);
	}

	[Fact]
	public void FromTrash_ShouldThrow_WhenImplementationIsUnknown()
	{
		var format = new TrashFormat("Test", false,
		[
			new TrashSpecification("Spec", "MysterySpecification", false, true, Fields("value", Json("x")))
		]);

		Should.Throw<KeyNotFoundException>(() => TrashCustomFormatJson.FromTrash(format));
	}

	[Fact]
	public void FromTrash_ShouldMapSizeAndYearRanges()
	{
		var format = new TrashFormat("Sized", false,
		[
			new TrashSpecification("Size", "SizeSpecification", false, true, Range(5, 15)),
			new TrashSpecification("Year", "YearSpecification", false, true, Range(2000, 2010))
		]);

		var result = TrashCustomFormatJson.FromTrash(format);

		result.Specifications[0].Value!.Value.GetProperty("min").GetDouble().ShouldBe(5);
		result.Specifications[0].Value!.Value.GetProperty("max").GetDouble().ShouldBe(15);
		result.Specifications[1].Value!.Value.GetProperty("min").GetDouble().ShouldBe(2000);
	}

	[Fact]
	public void ToTrash_ShouldRoundTrip()
	{
		var original = TrashCustomFormatJson.FromTrash(JsonSerializer.Deserialize<TrashFormat>(TrashJson, TrashOptions)!);

		var roundTripped = TrashCustomFormatJson.FromTrash(TrashCustomFormatJson.ToTrash(original));

		roundTripped.Name.ShouldBe(original.Name);
		roundTripped.IncludeCustomFormatWhenRenaming.ShouldBe(original.IncludeCustomFormatWhenRenaming);
		roundTripped.Specifications.Count.ShouldBe(original.Specifications.Count);
		roundTripped.Specifications[0].Type.ShouldBe(original.Specifications[0].Type);
		roundTripped.Specifications[0].Value!.Value.GetString().ShouldBe(original.Specifications[0].Value!.Value.GetString());
		roundTripped.Specifications[1].Negate.ShouldBe(original.Specifications[1].Negate);
	}

	[Fact]
	public void ToTrash_ShouldExportSizeRanges()
	{
		var format = new CustomFormat
		{
			Name = "Sized",
			Specifications =
			[
				new CustomFormatSpecification("Size", CustomFormatSpecificationType.SIZE, false, true,
					JsonSerializer.SerializeToElement(new { min = 2.0, max = 8.0 }))
			]
		};

		var trash = TrashCustomFormatJson.ToTrash(format);

		trash.Specifications[0].Implementation.ShouldBe("SizeSpecification");
		trash.Specifications[0].Fields!.Value.GetProperty("min").GetDouble().ShouldBe(2);
		trash.Specifications[0].Fields!.Value.GetProperty("max").GetDouble().ShouldBe(8);
	}

	[Fact]
	public void ToTrash_ShouldRoundTrip_ReleaseTypeAndQualityModifier()
	{
		var format = new TrashFormat("Season pack, remux", false,
		[
			new TrashSpecification("Full season", "ReleaseTypeSpecification", false, true, Fields("value", Json("FULL_SEASON"))),
			new TrashSpecification("Remux", "QualityModifierSpecification", false, true, Fields("value", Json("BLURAY_REMUX")))
		]);

		var mapped = TrashCustomFormatJson.FromTrash(format);

		mapped.Specifications[0].Type.ShouldBe(CustomFormatSpecificationType.RELEASE_TYPE);
		mapped.Specifications[0].Value!.Value.GetString().ShouldBe("FULL_SEASON");
		mapped.Specifications[1].Type.ShouldBe(CustomFormatSpecificationType.QUALITY_MODIFIER);
		mapped.Specifications[1].Value!.Value.GetString().ShouldBe("BLURAY_REMUX");

		var roundTripped = TrashCustomFormatJson.FromTrash(TrashCustomFormatJson.ToTrash(mapped));
		roundTripped.Specifications[0].Type.ShouldBe(CustomFormatSpecificationType.RELEASE_TYPE);
		roundTripped.Specifications[1].Type.ShouldBe(CustomFormatSpecificationType.QUALITY_MODIFIER);
	}

	[Fact]
	public void SupportedImplementations_ShouldContainTheDocumentedSet()
	{
		TrashCustomFormatJson.SupportedImplementations.ShouldContain("ReleaseTitleSpecification");
		TrashCustomFormatJson.SupportedImplementations.ShouldContain("SizeSpecification");
		TrashCustomFormatJson.SupportedImplementations.ShouldContain("YearSpecification");
		TrashCustomFormatJson.SupportedImplementations.ShouldContain("IndexerFlagSpecification");
	}

	private static JsonElement Fields(string name, JsonElement value)
		=> JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement> { [name] = value });

	private static JsonElement Range(double? min, double? max)
		=> JsonSerializer.SerializeToElement(new { min, max });

	private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);
}
