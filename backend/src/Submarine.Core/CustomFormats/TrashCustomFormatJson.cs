using System.Text.Json;
using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Core.CustomFormats;

/// <summary>
///     One TRaSH format specification.
/// </summary>
/// <param name="Name">Name of the specification.</param>
/// <param name="Implementation">TRaSH implementation class name, for example ReleaseTitleSpecification.</param>
/// <param name="Negate">Whether the match result is inverted.</param>
/// <param name="Required">Whether the release must match for the format to apply.</param>
/// <param name="Fields">Implementation specific fields: value, or min and max for range implementations.</param>
public sealed record TrashSpecification(
	string Name,
	string Implementation,
	bool Negate,
	bool Required,
	JsonElement? Fields);

/// <summary>
///     A TRaSH custom format.
/// </summary>
/// <param name="Name">Name of the format.</param>
/// <param name="IncludeCustomFormatWhenRenaming">Whether the format name appears in renamed files.</param>
/// <param name="Specifications">The specifications of the format.</param>
public sealed record TrashFormat(
	string Name,
	bool IncludeCustomFormatWhenRenaming,
	IReadOnlyList<TrashSpecification> Specifications);

/// <summary>
///     Maps TRaSH compatible custom format JSON to <see cref="CustomFormat" /> entities and back.
/// </summary>
public static class TrashCustomFormatJson
{
	/// <summary>
	///     TRaSH implementation class names understood by the mapper.
	/// </summary>
	public static IReadOnlySet<string> SupportedImplementations { get; } = new HashSet<string>(StringComparer.Ordinal)
	{
		"ReleaseTitleSpecification",
		"ReleaseGroupSpecification",
		"LanguageSpecification",
		"SourceSpecification",
		"ResolutionSpecification",
		"EditionSpecification",
		"IndexerFlagSpecification",
		"SizeSpecification",
		"YearSpecification",
		"ProtocolSpecification",
		"ReleaseFlagSpecification",
		"HardcodedSubsSpecification",
		"StreamingProviderSpecification",
		"StreamingServiceSpecification",
		"ReleaseTypeSpecification",
		"QualityModifierSpecification"
	};

	/// <summary>
	///     Maps a TRaSH format to a custom format entity.
	/// </summary>
	/// <exception cref="KeyNotFoundException">The implementation class name is unknown.</exception>
	public static CustomFormat FromTrash(TrashFormat format)
		=> new()
		{
			Name = format.Name,
			IncludeCustomFormatWhenRenaming = format.IncludeCustomFormatWhenRenaming,
			Specifications = [.. (format.Specifications ?? []).Select(FromTrash)]
		};

	/// <summary>
	///     Maps a custom format entity to its TRaSH representation.
	/// </summary>
	public static TrashFormat ToTrash(CustomFormat format)
		=> new(
			format.Name,
			format.IncludeCustomFormatWhenRenaming,
			[.. format.Specifications.Select(ToTrash)]);

	private static CustomFormatSpecification FromTrash(TrashSpecification specification)
	{
		var type = specification.Implementation switch
		{
			"ReleaseTitleSpecification" => CustomFormatSpecificationType.RELEASE_TITLE,
			"ReleaseGroupSpecification" => CustomFormatSpecificationType.RELEASE_GROUP,
			"LanguageSpecification" => CustomFormatSpecificationType.LANGUAGE,
			"SourceSpecification" => CustomFormatSpecificationType.QUALITY_SOURCE,
			"ResolutionSpecification" => CustomFormatSpecificationType.RESOLUTION,
			"EditionSpecification" => CustomFormatSpecificationType.EDITION,
			"IndexerFlagSpecification" => CustomFormatSpecificationType.INDEXER_FLAG,
			"SizeSpecification" => CustomFormatSpecificationType.SIZE,
			"YearSpecification" => CustomFormatSpecificationType.YEAR,
			"ProtocolSpecification" => CustomFormatSpecificationType.PROTOCOL,
			"ReleaseFlagSpecification" => CustomFormatSpecificationType.RELEASE_FLAG,
			"HardcodedSubsSpecification" => CustomFormatSpecificationType.HARDCODED_SUBS,
			"StreamingProviderSpecification" or "StreamingServiceSpecification"
				=> CustomFormatSpecificationType.STREAMING_PROVIDER,
			"ReleaseTypeSpecification" => CustomFormatSpecificationType.RELEASE_TYPE,
			"QualityModifierSpecification" => CustomFormatSpecificationType.QUALITY_MODIFIER,
			_ => throw new KeyNotFoundException(
				$"Unknown custom format specification implementation '{specification.Implementation}'")
		};

		return new CustomFormatSpecification(
			specification.Name,
			type,
			specification.Negate,
			specification.Required,
			ReadValue(type, specification.Fields));
	}

	private static TrashSpecification ToTrash(CustomFormatSpecification specification)
	{
		var implementation = specification.Type switch
		{
			CustomFormatSpecificationType.RELEASE_TITLE => "ReleaseTitleSpecification",
			CustomFormatSpecificationType.RELEASE_GROUP => "ReleaseGroupSpecification",
			CustomFormatSpecificationType.LANGUAGE => "LanguageSpecification",
			CustomFormatSpecificationType.QUALITY_SOURCE => "SourceSpecification",
			CustomFormatSpecificationType.RESOLUTION => "ResolutionSpecification",
			CustomFormatSpecificationType.EDITION => "EditionSpecification",
			CustomFormatSpecificationType.INDEXER_FLAG => "IndexerFlagSpecification",
			CustomFormatSpecificationType.SIZE => "SizeSpecification",
			CustomFormatSpecificationType.YEAR => "YearSpecification",
			CustomFormatSpecificationType.PROTOCOL => "ProtocolSpecification",
			CustomFormatSpecificationType.RELEASE_FLAG => "ReleaseFlagSpecification",
			CustomFormatSpecificationType.HARDCODED_SUBS => "HardcodedSubsSpecification",
			CustomFormatSpecificationType.STREAMING_PROVIDER => "StreamingProviderSpecification",
			CustomFormatSpecificationType.RELEASE_TYPE => "ReleaseTypeSpecification",
			CustomFormatSpecificationType.QUALITY_MODIFIER => "QualityModifierSpecification",
			_ => throw new ArgumentOutOfRangeException(nameof(specification), specification.Type, "Unknown specification type")
		};

		return new TrashSpecification(
			specification.Name,
			implementation,
			specification.Negate,
			specification.Required,
			ToFields(specification.Value));
	}

	private static JsonElement? ReadValue(CustomFormatSpecificationType type, JsonElement? fields)
	{
		if (fields is not { ValueKind: JsonValueKind.Object } element)
		{
			return type is CustomFormatSpecificationType.SIZE or CustomFormatSpecificationType.YEAR ? RangeElement(null, null) : null;
		}

		if (type is CustomFormatSpecificationType.SIZE or CustomFormatSpecificationType.YEAR)
		{
			return RangeElement(ReadNumber(element, "min"), ReadNumber(element, "max"));
		}

		if (type is CustomFormatSpecificationType.HARDCODED_SUBS)
		{
			return element.TryGetProperty("value", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False
				? JsonSerializer.SerializeToElement(flag.GetBoolean())
				: JsonSerializer.SerializeToElement(true);
		}

		return element.TryGetProperty("value", out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
			? JsonSerializer.SerializeToElement(value.GetString() ?? string.Empty)
			: null;
	}

	private static JsonElement? ToFields(JsonElement? value)
	{
		if (value is not { } element)
		{
			return null;
		}

		if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty("min", out _))
		{
			return element.Clone();
		}

		if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty("value", out var inner))
		{
			return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["value"] = inner.Clone() });
		}

		return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["value"] = element.Clone() });
	}

	private static JsonElement RangeElement(double? min, double? max)
	{
		var fields = new Dictionary<string, object?>();

		if (min is { } minValue)
		{
			fields["min"] = minValue;
		}

		if (max is { } maxValue)
		{
			fields["max"] = maxValue;
		}

		return JsonSerializer.SerializeToElement(fields);
	}

	private static double? ReadNumber(JsonElement element, string name)
		=> element.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.Number
			? property.GetDouble()
			: null;
}
