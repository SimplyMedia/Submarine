using System.Text.Json;
using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Core.AutoTagging;

/// <summary>
///     Evaluates auto tagging rule specifications against a series or movie.
///     A rule matches when every required specification matches and, following the same grouping rule as custom
///     formats, every specification type group containing non required specifications has at least one matching one.
/// </summary>
public static class AutoTaggingCalculator
{
	/// <summary>
	///     Whether the rule matches the item described by the context.
	/// </summary>
	public static bool Match(AutoTaggingRule rule, AutoTaggingContext context)
	{
		var specifications = rule.Specifications;

		foreach (var required in specifications.Where(specification => specification.Required))
		{
			if (!Evaluate(required, context))
			{
				return false;
			}
		}

		foreach (var group in specifications.Where(specification => !specification.Required).GroupBy(specification => specification.Type))
		{
			if (!group.Any(specification => Evaluate(specification, context)))
			{
				return false;
			}
		}

		return true;
	}

	private static bool Evaluate(AutoTaggingSpecification specification, AutoTaggingContext context)
	{
		var result = MatchesSpecification(specification, context);

		return specification.Negate ? !result : result;
	}

	private static bool MatchesSpecification(AutoTaggingSpecification specification, AutoTaggingContext context)
		=> specification.Type switch
		{
			AutoTaggingSpecificationType.GENRE => ReadStrings(specification.Value).Any(genre => context.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase)),
			AutoTaggingSpecificationType.ROOT_FOLDER => ReadNumber(specification.Value) is { } rootFolderId && context.RootFolderIds.Contains((int)rootFolderId),
			AutoTaggingSpecificationType.SERIES_TYPE => context.SeriesType is { } seriesType && TryEnum<SeriesType>(specification, out var value) && seriesType == value,
			AutoTaggingSpecificationType.STATUS => specification.Value is { ValueKind: JsonValueKind.String } status
				&& string.Equals(status.GetString(), context.Status, StringComparison.OrdinalIgnoreCase),
			AutoTaggingSpecificationType.YEAR => MatchesYear(specification, context),
			AutoTaggingSpecificationType.QUALITY_PROFILE => ReadNumber(specification.Value) is { } qualityProfileId && context.QualityProfileIds.Contains((int)qualityProfileId),
			AutoTaggingSpecificationType.MONITORED => context.Monitored,
			AutoTaggingSpecificationType.NETWORK_OR_STUDIO => context.NetworkOrStudio is { } networkOrStudio
				&& ReadStrings(specification.Value).Any(value => string.Equals(value, networkOrStudio, StringComparison.OrdinalIgnoreCase)),
			_ => false
		};

	private static bool MatchesYear(AutoTaggingSpecification specification, AutoTaggingContext context)
	{
		if (context.Year is not { } year)
		{
			return false;
		}

		var (min, max) = ReadRange(specification.Value);

		return (min is null || year >= min) && (max is null || year <= max);
	}

	private static (double? Min, double? Max) ReadRange(JsonElement? value)
	{
		if (value is not { ValueKind: JsonValueKind.Object } element)
		{
			return (null, null);
		}

		return (ReadNumber(element, "min"), ReadNumber(element, "max"));
	}

	private static double? ReadNumber(JsonElement element, string name)
		=> element.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.Number
			? property.GetDouble()
			: null;

	private static double? ReadNumber(JsonElement? value)
		=> value is { ValueKind: JsonValueKind.Number } element ? element.GetDouble() : null;

	private static IReadOnlyList<string> ReadStrings(JsonElement? value)
	{
		if (value is not { ValueKind: JsonValueKind.Array } element)
		{
			return [];
		}

		return [.. element.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)];
	}

	private static bool TryEnum<T>(AutoTaggingSpecification specification, out T value)
		where T : struct, Enum
	{
		if (specification.Value is { ValueKind: JsonValueKind.String } element
		    && Enum.TryParse(element.GetString(), ignoreCase: true, out value))
		{
			return true;
		}

		value = default;

		return false;
	}
}
