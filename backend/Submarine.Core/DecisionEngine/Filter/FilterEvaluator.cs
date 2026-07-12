namespace Submarine.Core.DecisionEngine.Filter;

/// <summary>
///     The result of evaluating a set of <see cref="ReleaseFilter" /> against a <see cref="FilterContext" />
/// </summary>
/// <param name="Rejections">Human-readable reasons the Release was rejected, empty if accepted</param>
/// <param name="Score">The accumulated tier-based score of matching <see cref="FilterMode.PREFER" /> filters</param>
public record FilterResult(IReadOnlyList<string> Rejections, int Score);

/// <summary>
///     Service which evaluates <see cref="ReleaseFilter" /> against a <see cref="FilterContext" />
/// </summary>
public class FilterEvaluator
{
	/// <summary>
	///     The base a <see cref="ReleaseFilter.Tier" /> is subtracted from before weighting
	/// </summary>
	public const int TierScoreBase = 10;

	/// <summary>
	///     The weight applied to a tier score
	/// </summary>
	public const int TierWeight = 100;

	/// <summary>
	///     Evaluates the given Filters against a candidate's Field values.
	///     An <see cref="FilterMode.ALLOW" /> filter on a Field the Release does not populate (e.g. a null release group
	///     or source) never matches, so the Release is rejected for that Field.
	/// </summary>
	/// <param name="ctx">The candidate's Field values</param>
	/// <param name="filters">The Filters to evaluate</param>
	/// <returns>The <see cref="FilterResult" /> of the evaluation</returns>
	public FilterResult Evaluate(FilterContext ctx, IReadOnlyCollection<ReleaseFilter> filters)
	{
		var rejections = new List<string>();
		var score = 0;

		foreach (var group in filters.Where(f => f.Mode == FilterMode.ALLOW).GroupBy(f => f.Field))
		{
			var values = FieldValues(ctx, group.Key);

			if (!group.Any(f => Matches(values, f)))
				rejections.Add(values.Count == 0
					? $"no {FieldName(group.Key)}"
					: $"{FieldName(group.Key)} not allowed");
		}

		foreach (var filter in filters.Where(f => f.Mode == FilterMode.BLOCK))
			if (Matches(FieldValues(ctx, filter.Field), filter))
				rejections.Add($"blocked by {FieldName(filter.Field)} filter");

		foreach (var group in filters.Where(f => f.Mode == FilterMode.PREFER).GroupBy(f => f.Field))
		{
			var values = FieldValues(ctx, group.Key);
			var matched = group.Where(f => Matches(values, f)).ToList();

			if (matched.Count != 0)
				score += (TierScoreBase - matched.Min(f => f.Tier)) * TierWeight;
		}

		return new FilterResult(rejections, score);
	}

	private static bool Matches(IReadOnlyList<string> fieldValues, ReleaseFilter filter)
		=> fieldValues.Any(v => filter.Values.Any(fv => string.Equals(v, fv, StringComparison.OrdinalIgnoreCase)));

	private static IReadOnlyList<string> FieldValues(FilterContext ctx, FilterField field)
		=> field switch
		{
			FilterField.RELEASE_GROUP => Single(ctx.ReleaseGroup),
			FilterField.INDEXER => Single(ctx.IndexerName),
			FilterField.QUALITY => Single(ctx.Quality),
			FilterField.LANGUAGE => ctx.Languages.Select(l => l.ToString()).ToList(),
			FilterField.SOURCE => Single(ctx.Source?.ToString()),
			_ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown filter field")
		};

	private static IReadOnlyList<string> Single(string? value)
		=> value is null ? Array.Empty<string>() : new[] { value };

	private static string FieldName(FilterField field)
		=> field switch
		{
			FilterField.RELEASE_GROUP => "release group",
			FilterField.INDEXER => "indexer",
			FilterField.QUALITY => "quality",
			FilterField.LANGUAGE => "language",
			FilterField.SOURCE => "source",
			_ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown filter field")
		};
}
