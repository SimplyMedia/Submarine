using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release.Torrent;

namespace Submarine.Core.CustomFormats;

/// <summary>
///     Evaluates custom format specifications against a release and scores formats inside a quality profile.
///     A format matches when every required specification matches and, following the Sonarr grouping rule, every
///     specification type group containing non required specifications has at least one matching one.
/// </summary>
public static class CustomFormatCalculator
{
	private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new();

	/// <summary>
	///     Whether the format matches the release described by the context.
	/// </summary>
	public static bool Match(CustomFormat format, ReleaseContext context)
	{
		var specifications = format.Specifications;

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

	/// <summary>
	///     Returns the formats matching the release described by the context.
	/// </summary>
	public static IReadOnlyList<CustomFormat> Match(IReadOnlyCollection<CustomFormat> formats, ReleaseContext context)
		=> formats.Where(format => Match(format, context)).ToList();

	/// <summary>
	///     Sums the profile scores of the matched formats.
	/// </summary>
	public static int Score(QualityProfile profile, IReadOnlyCollection<CustomFormat> matchedFormats)
		=> matchedFormats.Sum(format => profile.FormatItems
			.FirstOrDefault(item => item.CustomFormatId == format.Id)?.Score ?? 0);

	private static bool Evaluate(CustomFormatSpecification specification, ReleaseContext context)
	{
		var result = MatchesSpecification(specification, context);

		return specification.Negate ? !result : result;
	}

	private static bool MatchesSpecification(CustomFormatSpecification specification, ReleaseContext context)
	{
		var release = context.Release;

		return specification.Type switch
		{
			CustomFormatSpecificationType.RELEASE_TITLE => MatchesRegex(specification, context.Title),
			CustomFormatSpecificationType.RELEASE_GROUP => release.ReleaseGroup is { } group && MatchesRegex(specification, group),
			CustomFormatSpecificationType.LANGUAGE => TryEnum<Language>(specification, out var language)
			                                          && release.Languages.Contains(language),
			CustomFormatSpecificationType.QUALITY_SOURCE => TryEnum<QualitySource>(specification, out var source)
			                                                && release.Quality.Resolution.Source == source,
			CustomFormatSpecificationType.RESOLUTION => TryEnum<QualityResolution>(specification, out var resolution)
			                                           && release.Quality.Resolution.Resolution == resolution,
			CustomFormatSpecificationType.STREAMING_PROVIDER => TryEnum<StreamingProvider>(specification, out var provider)
			                                                   && release.StreamingProvider == provider,
			CustomFormatSpecificationType.EDITION => release.MovieReleaseData?.Edition is { } edition
			                                         && MatchesRegex(specification, edition),
			CustomFormatSpecificationType.RELEASE_FLAG => release is TorrentRelease torrent
			                                             && TryEnum<TorrentReleaseFlags>(specification, out var flag)
			                                             && torrent.Flags.HasFlag(flag),
			CustomFormatSpecificationType.PROTOCOL => TryEnum<Protocol>(specification, out var protocol)
			                                          && (context.Protocol ?? release.Protocol) == protocol,
			CustomFormatSpecificationType.HARDCODED_SUBS => ReadBool(specification.Value) is not false && release.HardcodedSubs,
			CustomFormatSpecificationType.SIZE => MatchesSize(specification, context),
			CustomFormatSpecificationType.YEAR => MatchesYear(specification, context),
			CustomFormatSpecificationType.INDEXER_FLAG => TryEnum<IndexerFlag>(specification, out var indexerFlag)
			                                             && context.IndexerFlags?.Contains(indexerFlag) == true,
			_ => false
		};
	}

	private static bool MatchesRegex(CustomFormatSpecification specification, string input)
	{
		if (specification.Value is not { ValueKind: JsonValueKind.String } value)
		{
			return false;
		}

		try
		{
			return GetRegex(value.GetString()!)?.IsMatch(input) ?? false;
		}
		catch (RegexMatchTimeoutException)
		{
			return false;
		}
	}

	private static bool MatchesSize(CustomFormatSpecification specification, ReleaseContext context)
	{
		if (context.Size is not { } bytes)
		{
			return false;
		}

		var (min, max) = ReadRange(specification.Value);
		var sizeGb = bytes / 1024d / 1024d / 1024d;

		return (min is null || sizeGb >= min) && (max is null || sizeGb <= max);
	}

	private static bool MatchesYear(CustomFormatSpecification specification, ReleaseContext context)
	{
		var year = context.Year ?? context.Release.Year;

		if (year is null)
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

	private static bool? ReadBool(JsonElement? value)
		=> value is { ValueKind: JsonValueKind.True or JsonValueKind.False } element && element.GetBoolean();

	private static bool TryEnum<T>(CustomFormatSpecification specification, out T value)
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

	private static Regex? GetRegex(string pattern)
		=> RegexCache.GetOrAdd(pattern, static p =>
		{
			try
			{
				return new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
			}
			catch (ArgumentException)
			{
				return null;
			}
		});
}
