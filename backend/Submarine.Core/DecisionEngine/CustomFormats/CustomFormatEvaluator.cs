using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;

namespace Submarine.Core.DecisionEngine.CustomFormats;

/// <summary>
///     Service which evaluates which Custom Formats match a Release
/// </summary>
public class CustomFormatEvaluator
{
	private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

	private readonly ILogger<CustomFormatEvaluator> _logger;

	private readonly ConcurrentDictionary<(CustomFormatConditionType Type, string Value), bool> _unparsableValues = new();

	/// <summary>
	///     Creates a new <see cref="CustomFormatEvaluator" />
	/// </summary>
	/// <param name="logger">The logger of this <see cref="CustomFormatEvaluator" /></param>
	public CustomFormatEvaluator(ILogger<CustomFormatEvaluator> logger)
		=> _logger = logger;

	/// <summary>
	///     Evaluates which of the given Custom Formats match a Release
	/// </summary>
	/// <param name="release">The release to evaluate against</param>
	/// <param name="formats">The Custom Formats to evaluate</param>
	/// <returns>The Custom Formats matching the release</returns>
	public IReadOnlyList<CustomFormat> Evaluate(BaseRelease release, IReadOnlyCollection<CustomFormat> formats)
		=> formats.Where(format => Matches(release, format)).ToList();

	private bool Matches(BaseRelease release, CustomFormat format)
	{
		if (!format.Conditions.Where(condition => condition.Required)
			    .All(condition => IsSatisfied(release, condition)))
			return false;

		var optional = format.Conditions.Where(condition => !condition.Required).ToList();

		return optional.Count == 0 || optional.Any(condition => IsSatisfied(release, condition));
	}

	private bool IsSatisfied(BaseRelease release, CustomFormatCondition condition)
		=> condition.Negated
			? !MatchesCondition(release, condition)
			: MatchesCondition(release, condition);

	private bool MatchesCondition(BaseRelease release, CustomFormatCondition condition)
		=> condition.Type switch
		{
			CustomFormatConditionType.RELEASE_TITLE
				=> GetRegex(condition.Value).IsMatch(release.FullTitle),
			CustomFormatConditionType.RELEASE_GROUP
				=> release.ReleaseGroup != null && GetRegex(condition.Value).IsMatch(release.ReleaseGroup),
			CustomFormatConditionType.LANGUAGE
				=> TryParseEnum<Language>(condition, out var language) && release.Languages.Contains(language),
			CustomFormatConditionType.QUALITY_SOURCE
				=> TryParseEnum<QualitySource>(condition, out var source) && release.Quality.Resolution.Source == source,
			CustomFormatConditionType.RESOLUTION
				=> TryParseEnum<QualityResolution>(condition, out var resolution)
				   && release.Quality.Resolution.Resolution == resolution,
			CustomFormatConditionType.STREAMING_PROVIDER
				=> TryParseEnum<StreamingProvider>(condition, out var provider) && release.StreamingProvider == provider,
			CustomFormatConditionType.EDITION
				=> release.MovieReleaseData?.Edition != null
				   && GetRegex(condition.Value).IsMatch(release.MovieReleaseData.Edition),
			CustomFormatConditionType.RELEASE_FLAG
				=> release is TorrentRelease torrent && TryParseEnum<TorrentReleaseFlags>(condition, out var flag)
				                                     && torrent.Flags.HasFlag(flag),
			CustomFormatConditionType.PROTOCOL
				=> TryParseEnum<Protocol>(condition, out var protocol) && release.Protocol == protocol,
			_ => throw new ArgumentOutOfRangeException(nameof(condition), condition.Type, "Unknown condition type")
		};

	private bool TryParseEnum<T>(CustomFormatCondition condition, out T value) where T : struct, Enum
	{
		if (Enum.TryParse(condition.Value, true, out value))
			return true;

		if (_unparsableValues.TryAdd((condition.Type, condition.Value), true))
			_logger.LogWarning("Condition {Name} has unparsable value {Value} for type {Type}", condition.Name,
				condition.Value, condition.Type);

		return false;
	}

	private static Regex GetRegex(string pattern)
		=> RegexCache.GetOrAdd(pattern, static p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase));
}
