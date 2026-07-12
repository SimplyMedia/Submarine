using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Languages;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
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
	private const int PreferredProtocolBonus = 25;

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
	/// <param name="now">The point in time delay windows are evaluated against, defaults to the current UTC time</param>
	/// <returns>The <see cref="DownloadDecision" /> for the candidate</returns>
	public DownloadDecision Decide(ReleaseCandidate candidate, MediaContext ctx, DateTimeOffset? now = null)
	{
		var release = candidate.Release;
		var rejections = new List<RejectionReason>();
		var evaluatedAt = now ?? DateTimeOffset.UtcNow;

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

		var qualityUpgrade = false;
		var qualityNotWorse = true;

		if (ctx.ExistingFileQuality is { } existingQuality)
		{
			var existingIndex = items.FindIndex(i =>
				i.Quality.Source == existingQuality.Resolution.Source
				&& i.Quality.Resolution == existingQuality.Resolution.Resolution);

			// a proper/repack at the same quality tier is a valid upgrade even once the cutoff is met
			var revisionUpgradeAtSameQuality = existingIndex == qualityIndex
			                                   && release.Quality.Revision > existingQuality.Revision;

			qualityUpgrade = revisionUpgradeAtSameQuality
			                 || (!ctx.QualityProfile.MeetsCutoff(existingQuality)
			                     && ctx.QualityProfile.IsUpgrade(existingQuality, release.Quality));

			qualityNotWorse = qualityIndex >= existingIndex;
		}

		// an upgrade in either dimension is enough, but a quality downgrade is never traded for a language gain
		var languageUpgrade = ctx.ExistingFileLanguages is { } existingLanguages
		                      && !existingLanguages.Contains(ctx.LanguageProfile.Cutoff)
		                      && AddsLanguageImprovement(ctx.LanguageProfile, existingLanguages, release.Languages)
		                      && qualityNotWorse;

		if ((ctx.ExistingFileQuality is not null || ctx.ExistingFileLanguages is not null)
		    && !qualityUpgrade && !languageUpgrade)
		{
			if (ctx.ExistingFileQuality is { } existing && ctx.QualityProfile.MeetsCutoff(existing))
				rejections.Add(new RejectionReason("existing file already meets the quality cutoff",
					RejectionType.PERMANENT));
			else if (ctx.ExistingFileQuality is not null)
				rejections.Add(new RejectionReason("not an upgrade over the existing file",
					RejectionType.PERMANENT));
			else
				rejections.Add(new RejectionReason("existing file already meets the language cutoff",
					RejectionType.PERMANENT));
		}

		if (release.Protocol == Protocol.BITTORRENT
		    && candidate.MinimumSeeders is { } minimumSeeders
		    && candidate.Info.Seeders is { } seeders
		    && seeders < minimumSeeders)
			rejections.Add(new RejectionReason($"{seeders} seeders, minimum is {minimumSeeders}",
				RejectionType.TEMPORARY));

		if (IsWithinDelayWindow(candidate, ctx, qualityIndex, items, evaluatedAt))
			rejections.Add(new RejectionReason("waiting for delay window", RejectionType.TEMPORARY));

		foreach (var profile in ctx.ReleaseProfiles)
		{
			if (profile.Indexer is { } indexer && indexer != candidate.IndexerName)
				continue;

			foreach (var term in profile.Ignored)
				if (MatchesTerm(release.FullTitle, term))
					rejections.Add(new RejectionReason($"matches ignored term '{term}'", RejectionType.PERMANENT));

			foreach (var term in profile.Required)
				if (!MatchesTerm(release.FullTitle, term))
				{
					rejections.Add(new RejectionReason($"missing required term '{term}'", RejectionType.PERMANENT));
					break;
				}
		}

		if (ctx.MinimumAvailabilityMet == false)
			rejections.Add(new RejectionReason("minimum availability not met", RejectionType.TEMPORARY));

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
	/// <param name="now">The point in time delay windows are evaluated against, defaults to the current UTC time</param>
	/// <returns>The ordered <see cref="DownloadDecision" />s</returns>
	public IReadOnlyList<DownloadDecision> DecideAll(IReadOnlyCollection<ReleaseCandidate> candidates, MediaContext ctx,
		DateTimeOffset? now = null)
		=> candidates
			.Select(candidate => Decide(candidate, ctx, now))
			.OrderByDescending(decision => decision.Approved)
			.ThenByDescending(decision => decision.Score)
			.ToList();

	// a candidate on a Protocol the Delay Profile delays is held until its publish date clears the window, unless the
	// Profile bypasses the delay once the candidate already sits at the highest allowed Quality
	private static bool IsWithinDelayWindow(ReleaseCandidate candidate, MediaContext ctx, int qualityIndex,
		List<QualityProfileItem> items, DateTimeOffset now)
	{
		if (ctx.DelayProfile is not { } delay || candidate.Info.PublishDate is not { } published)
			return false;

		var delayMinutes = candidate.Release.Protocol switch
		{
			Protocol.BITTORRENT => delay.TorrentDelayMinutes,
			Protocol.USENET => delay.UsenetDelayMinutes,
			_ => 0
		};

		if (delayMinutes <= 0 || published.AddMinutes(delayMinutes) <= now)
			return false;

		return !(delay.BypassIfHighestQuality && qualityIndex == items.FindLastIndex(i => i.Allowed));
	}

	// a term matches as a case-insensitive substring, or as a regular expression when wrapped in /.../
	private static bool MatchesTerm(string title, string term)
		=> term is ['/', _, .., '/']
			? Regex.IsMatch(title, term[1..^1], RegexOptions.IgnoreCase)
			: title.Contains(term, StringComparison.OrdinalIgnoreCase);

	// candidate improves language when it holds a profile-allowed language ranked better than the best existing one
	private static bool AddsLanguageImprovement(LanguageProfile profile, IReadOnlyList<Language> existing,
		IReadOnlyList<Language> candidate)
	{
		if (!profile.UpgradeAllowed)
			return false;

		var existingRank = BestLanguageRank(profile, existing);
		var candidateRank = BestLanguageRank(profile, candidate);

		return candidateRank >= 0 && (existingRank < 0 || candidateRank < existingRank);
	}

	private static int BestLanguageRank(LanguageProfile profile, IReadOnlyList<Language> languages)
	{
		var best = -1;

		foreach (var language in languages)
		{
			var rank = profile.Languages.IndexOf(language);

			if (rank >= 0 && (best < 0 || rank < best))
				best = rank;
		}

		return best;
	}

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

		if (ctx.DelayProfile is { } delay && release.Protocol == delay.PreferredProtocol)
			score += PreferredProtocolBonus;

		return score;
	}
}
