using Microsoft.Extensions.Logging;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Release;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     Service which decides whether a candidate Release should be downloaded and scores it against a
///     <see cref="MediaContext" />
/// </summary>
public class DownloadDecisionService
{
	private const int QualityWeight = 1000;
	private const int RevisionWeight = 10;
	private const int ConsistentReleaseGroupBonus = 150;
	private const int FullSeasonBonus = 50;

	private readonly CustomFormatEvaluator _customFormatEvaluator;
	private readonly FilterEvaluator _filterEvaluator;
	private readonly ILogger<DownloadDecisionService> _logger;

	/// <summary>
	///     Creates a new <see cref="DownloadDecisionService" />
	/// </summary>
	/// <param name="logger">The logger of this service</param>
	/// <param name="filterEvaluator">The <see cref="FilterEvaluator" /> used to apply Filters</param>
	/// <param name="customFormatEvaluator">The <see cref="CustomFormatEvaluator" /> used to score Custom Formats</param>
	public DownloadDecisionService(
		ILogger<DownloadDecisionService> logger,
		FilterEvaluator filterEvaluator,
		CustomFormatEvaluator customFormatEvaluator)
	{
		_logger = logger;
		_filterEvaluator = filterEvaluator;
		_customFormatEvaluator = customFormatEvaluator;
	}

	/// <summary>
	///     Decides whether a candidate Release should be downloaded and scores it
	/// </summary>
	/// <param name="candidate">The candidate Release</param>
	/// <param name="ctx">The context the decision is made in</param>
	/// <returns>The <see cref="DownloadDecision" /> for the candidate</returns>
	public DownloadDecision Decide(ReleaseCandidate candidate, MediaContext ctx)
	{
		var release = candidate.Release;
		var rejections = new List<RejectionReason>();

		var items = ctx.QualityProfile.Items;
		var qualityIndex = items.FindIndex(i =>
			i.Quality.Source == release.Quality.Resolution.Source
			&& i.Quality.Resolution == release.Quality.Resolution.Resolution);

		if (qualityIndex < 0 || !items[qualityIndex].Allowed)
			rejections.Add(new RejectionReason(
				$"quality {release.Quality.Resolution.Name} is not allowed by the quality profile",
				RejectionType.PERMANENT));

		if (!release.Languages.Any(l => ctx.LanguageProfile.Languages.Contains(l)))
			rejections.Add(new RejectionReason("no wanted language present", RejectionType.PERMANENT));

		if (ctx.ExistingFileQuality is not null)
		{
			if (ctx.QualityProfile.MeetsCutoff(ctx.ExistingFileQuality))
				rejections.Add(new RejectionReason("existing file already meets the quality cutoff",
					RejectionType.PERMANENT));
			else if (!ctx.QualityProfile.IsUpgrade(ctx.ExistingFileQuality, release.Quality))
				rejections.Add(new RejectionReason("not an upgrade over the existing file",
					RejectionType.PERMANENT));
		}

		var filterResult = _filterEvaluator.Evaluate(FilterContext.From(release, candidate.IndexerName), ctx.Filters);

		foreach (var rejection in filterResult.Rejections)
			rejections.Add(new RejectionReason(rejection, RejectionType.PERMANENT));

		var matchedFormats = _customFormatEvaluator.Evaluate(release, ctx.CustomFormats);

		var approved = rejections.Count == 0;
		var score = approved
			? Score(candidate, ctx, qualityIndex, filterResult.Score, matchedFormats)
			: 0;

		if (!approved)
			_logger.LogDebug("Rejected {Title}: {Reasons}", release.FullTitle,
				string.Join(", ", rejections.Select(r => r.Reason)));

		return new DownloadDecision(candidate, approved, score, rejections,
			matchedFormats.Select(f => f.Name).ToList());
	}

	/// <summary>
	///     Decides and scores all candidates, ordered best-first (approved first, then by score descending)
	/// </summary>
	/// <param name="candidates">The candidate Releases</param>
	/// <param name="ctx">The context the decision is made in</param>
	/// <returns>The ordered <see cref="DownloadDecision" />s</returns>
	public IReadOnlyList<DownloadDecision> DecideAll(IReadOnlyCollection<ReleaseCandidate> candidates, MediaContext ctx)
		=> candidates
			.Select(candidate => Decide(candidate, ctx))
			.OrderByDescending(decision => decision.Approved)
			.ThenByDescending(decision => decision.Score)
			.ToList();

	private static int Score(ReleaseCandidate candidate, MediaContext ctx, int qualityIndex, int filterScore,
		IReadOnlyList<CustomFormat> matchedFormats)
	{
		var release = candidate.Release;

		var score = qualityIndex * QualityWeight
		            + release.Quality.Revision.Version * RevisionWeight
		            + filterScore
		            + matchedFormats.Sum(f => ctx.CustomFormatScores.TryGetValue(f.Id, out var s) ? s : 0);

		if (ctx.SeasonReleaseGroup is not null
		    && string.Equals(ctx.SeasonReleaseGroup, release.ReleaseGroup, StringComparison.OrdinalIgnoreCase))
			score += ConsistentReleaseGroupBonus;

		score -= candidate.IndexerPriority;

		if (release.SeriesReleaseData?.ReleaseType == SeriesReleaseType.FULL_SEASON)
			score += FullSeasonBonus;

		return score;
	}
}
