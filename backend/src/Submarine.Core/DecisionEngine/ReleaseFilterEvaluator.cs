using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     The result of evaluating release filters: rejection reasons and the accumulated tier score of matching prefer
///     filters.
/// </summary>
/// <param name="Rejections">Human-readable reasons the release was rejected, empty when accepted.</param>
/// <param name="Score">The accumulated tier score of matching PREFER filters.</param>
public sealed record ReleaseFilterResult(IReadOnlyList<string> Rejections, int Score);

/// <summary>
///     Evaluates <see cref="ReleaseFilter" /> rules against a candidate release.
/// </summary>
public sealed class ReleaseFilterEvaluator
{
	/// <summary>The base a filter tier is subtracted from before weighting.</summary>
	public const int TierScoreBase = 10;

	/// <summary>The weight applied to a tier score.</summary>
	public const int TierWeight = 100;

	/// <summary>
	///     Evaluates the given filters against a candidate's field values. An ALLOW filter on a field the release does not
	///     populate never matches, so the release is rejected for that field.
	/// </summary>
	public ReleaseFilterResult Evaluate(
		ReleaseCandidate candidate,
		IReadOnlyCollection<ReleaseFilter> filters)
	{
		var release = candidate.Parsed;
		var rejections = new List<string>();
		var score = 0;

		foreach (var group in filters.Where(filter => filter.Mode == ReleaseFilterMode.ALLOW).GroupBy(filter => filter.Field))
		{
			var values = FieldValues(candidate, group.Key);

			if (!group.Any(filter => Matches(values, filter)))
			{
				rejections.Add(values.Count == 0 ? $"no {FieldName(group.Key)}" : $"{FieldName(group.Key)} not allowed");
			}
		}

		foreach (var filter in filters.Where(filter => filter.Mode == ReleaseFilterMode.BLOCK))
		{
			if (Matches(FieldValues(candidate, filter.Field), filter))
			{
				rejections.Add($"blocked by {FieldName(filter.Field)} filter");
			}
		}

		foreach (var group in filters.Where(filter => filter.Mode == ReleaseFilterMode.PREFER).GroupBy(filter => filter.Field))
		{
			var values = FieldValues(candidate, group.Key);
			var matched = group.Where(filter => Matches(values, filter)).ToList();

			if (matched.Count != 0)
			{
				score += (TierScoreBase - matched.Min(filter => filter.Tier)) * TierWeight;
			}
		}

		return new ReleaseFilterResult(rejections, score);
	}

	private static bool Matches(IReadOnlyList<string> fieldValues, ReleaseFilter filter)
		=> fieldValues.Any(value => filter.Values.Any(filterValue =>
			string.Equals(value, filterValue, StringComparison.OrdinalIgnoreCase)));

	private static IReadOnlyList<string> FieldValues(ReleaseCandidate candidate, ReleaseFilterField field)
		=> field switch
		{
			ReleaseFilterField.RELEASE_GROUP => Single(candidate.Parsed.ReleaseGroup),
			ReleaseFilterField.INDEXER => Single(candidate.Info.Indexer),
			ReleaseFilterField.QUALITY => Single(candidate.Parsed.Quality.Resolution.Name),
			ReleaseFilterField.LANGUAGE => [.. candidate.Parsed.Languages.Select(language => language.ToString())],
			ReleaseFilterField.SOURCE => Single(candidate.Parsed.Quality.Resolution.Source?.ToString()),
			_ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown filter field")
		};

	private static IReadOnlyList<string> Single(string? value)
		=> value is null ? [] : [value];

	private static string FieldName(ReleaseFilterField field) => field switch
	{
		ReleaseFilterField.RELEASE_GROUP => "release group",
		ReleaseFilterField.INDEXER => "indexer",
		ReleaseFilterField.QUALITY => "quality",
		ReleaseFilterField.LANGUAGE => "language",
		ReleaseFilterField.SOURCE => "source",
		_ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown filter field")
	};
}
