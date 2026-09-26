using Submarine.Core.CustomFormats;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Profiles;
using Submarine.Core.Provider;
using Submarine.Core.Release;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     Decides whether a candidate release should be downloaded for a media version and scores it:
///     quality tier, revision, custom format scores, filter tier score, release group consistency,
///     season packs, protocol preference and indexer priority.
/// </summary>
public sealed class DownloadDecisionMaker : IDownloadDecisionMaker
{
	private const int QualityWeight = 1000;
	private const int RevisionWeight = 10;
	private const int ConsistentReleaseGroupBonus = 150;
	private const int FullSeasonBonus = 50;
	private const int PreferredProtocolBonus = 25;

	private static readonly ReleaseComparer ComparerInstance = new();

	private readonly ReleaseFilterEvaluator _filterEvaluator;

	/// <summary>
	///     Creates a new <see cref="DownloadDecisionMaker" />.
	/// </summary>
	/// <param name="filterEvaluator">The evaluator applying release filters.</param>
	public DownloadDecisionMaker(ReleaseFilterEvaluator filterEvaluator)
		=> _filterEvaluator = filterEvaluator;

	/// <inheritdoc />
	public DownloadDecision Decide(ReleaseCandidate candidate, DecisionContext context, DateTime? now = null)
	{
		var release = candidate.Parsed;
		var evaluatedAt = now ?? DateTime.UtcNow;
		var rejections = new List<RejectionReason>();

		var releaseContext = new ReleaseContext(
			release.FullTitle,
			release,
			candidate.Info.Size,
			Year: null,
			candidate.Info.IndexerFlags,
			release.Protocol,
			candidate.Info.Indexer);

		var matchedFormats = CustomFormatCalculator.Match(context.CustomFormats, releaseContext);
		var customFormatScore = CustomFormatCalculator.Score(context.QualityProfile, matchedFormats);

		var qualityIndex = context.QualityProfile.GetIndex(release.Quality);

		if (qualityIndex < 0 || !context.QualityProfile.Items[qualityIndex].Allowed)
		{
			rejections.Add(new RejectionReason(
				$"quality {release.Quality.Resolution.Name} is not allowed by the quality profile",
				RejectionType.PERMANENT));
		}

		if (!context.LanguageProfile.IsWanted(release.Languages))
		{
			rejections.Add(new RejectionReason("no wanted language present", RejectionType.PERMANENT));
		}

		var qualityUpgrade = false;
		var qualityNotWorse = true;

		if (context.ExistingFileQuality is { } existingQuality)
		{
			var existingIndex = context.QualityProfile.GetIndex(existingQuality);

			qualityUpgrade = context.QualityProfile.IsQualityUpgrade(
				existingQuality,
				release.Quality,
				context.ExistingCustomFormatScore,
				customFormatScore,
				context.QualityProfile.UpgradeAllowed,
				context.DownloadPropersAndRepacks);

			qualityNotWorse = qualityIndex >= existingIndex;
		}

		// an upgrade in either dimension is enough, but a quality downgrade is never traded for a language gain
		var languageUpgrade = context.ExistingFileLanguages is { } existingLanguages
		                      && !context.LanguageProfile.MeetsCutoff(existingLanguages)
		                      && context.LanguageProfile.IsUpgrade(existingLanguages, release.Languages)
		                      && qualityNotWorse;

		if ((context.ExistingFileQuality is not null || context.ExistingFileLanguages is not null)
		    && !qualityUpgrade && !languageUpgrade)
		{
			if (context.ExistingFileQuality is { } existing && context.QualityProfile.MeetsCutoff(existing, context.ExistingCustomFormatScore))
			{
				rejections.Add(new RejectionReason("existing file already meets the quality cutoff", RejectionType.PERMANENT));
			}
			else if (context.ExistingFileQuality is not null)
			{
				rejections.Add(new RejectionReason("not an upgrade over the existing file", RejectionType.PERMANENT));
			}
			else
			{
				rejections.Add(new RejectionReason("existing file already meets the language cutoff", RejectionType.PERMANENT));
			}
		}

		var queued = context.QueuedReleases.FirstOrDefault(queuedRelease => (candidate.MatchedMovieId is { } movieId && queuedRelease.MovieId == movieId)
			|| (candidate.EpisodeIds is { Count: > 0 } episodeIds && queuedRelease.EpisodeIds.Any(episodeIds.Contains)));

		if (queued is not null
		    && !context.QualityProfile.IsQualityUpgrade(
			    queued.Quality, release.Quality, queued.CustomFormatScore, customFormatScore,
			    context.QualityProfile.UpgradeAllowed, context.DownloadPropersAndRepacks))
		{
			rejections.Add(new RejectionReason("release is already in the queue", RejectionType.TEMPORARY));
		}

		if (customFormatScore < context.QualityProfile.MinFormatScore)
		{
			rejections.Add(new RejectionReason(
				$"custom format score {customFormatScore} is below the minimum of {context.QualityProfile.MinFormatScore}",
				RejectionType.PERMANENT));
		}

		var definition = context.QualityDefinitions.FirstOrDefault(quality => quality.Source == release.Quality.Resolution.Source
		                                                                     && quality.Resolution == release.Quality.Resolution.Resolution);
		if (!definition.IsWithinSize(candidate.Info.Size, context.RuntimeMinutes, context.EpisodeCount))
		{
			rejections.Add(new RejectionReason(
				$"size {candidate.Info.Size / 1024d / 1024d:0.0} MB is outside the limits of quality {release.Quality.Resolution.Name}",
				RejectionType.PERMANENT));
		}

		if (release.Protocol == Protocol.BITTORRENT
		    && candidate.MinimumSeeders is { } minimumSeeders
		    && candidate.Info.Seeders is { } seeders
		    && seeders < minimumSeeders)
		{
			rejections.Add(new RejectionReason($"{seeders} seeders, minimum is {minimumSeeders}", RejectionType.TEMPORARY));
		}

		if (IsWithinDelayWindow(candidate, context, qualityIndex, customFormatScore, evaluatedAt))
		{
			rejections.Add(new RejectionReason("waiting for delay window", RejectionType.TEMPORARY));
		}

		foreach (var profile in context.ReleaseProfiles.Where(profile => profile.Enabled))
		{
			if (profile.IndexerId is { } indexerId && indexerId != candidate.Info.IndexerId)
			{
				continue;
			}

			foreach (var term in profile.Ignored)
			{
				if (MatchesTerm(release.FullTitle, term))
				{
					rejections.Add(new RejectionReason($"matches ignored term '{term}'", RejectionType.PERMANENT));
				}
			}

			foreach (var term in profile.Required)
			{
				if (!MatchesTerm(release.FullTitle, term))
				{
					rejections.Add(new RejectionReason($"missing required term '{term}'", RejectionType.PERMANENT));
					break;
				}
			}
		}

		if (context.MinimumAvailabilityMet == false)
		{
			rejections.Add(new RejectionReason("minimum availability not met", RejectionType.TEMPORARY));
		}

		var filterResult = _filterEvaluator.Evaluate(candidate, context.ReleaseFilters);

		foreach (var rejection in filterResult.Rejections)
		{
			rejections.Add(new RejectionReason(rejection, RejectionType.PERMANENT));
		}

		if (context.IsBlocklisted?.Invoke(candidate) == true)
		{
			rejections.Add(new RejectionReason("release is blocklisted", RejectionType.PERMANENT));
		}

		if (context.IndexerConfig is { } config)
		{
			rejections.AddRange(ApplyIndexerConfig(config, candidate, evaluatedAt));
		}

		var approved = rejections.Count == 0;
		var score = approved ? Score(candidate, context, qualityIndex, filterResult.Score, customFormatScore) : 0;

		return new DownloadDecision(candidate, approved, score, rejections, matchedFormats, customFormatScore);
	}

	/// <inheritdoc />
	public IReadOnlyList<DownloadDecision> DecideAll(
		IReadOnlyCollection<ReleaseCandidate> candidates,
		DecisionContext context,
		DateTime? now = null)
		=> candidates
			.Select(candidate => Decide(candidate, context, now))
			.OrderBy(decision => decision, ComparerInstance)
			.ToList();

	private static IReadOnlyList<RejectionReason> ApplyIndexerConfig(
		IndexerConfig config,
		ReleaseCandidate candidate,
		DateTime evaluatedAt)
	{
		var rejections = new List<RejectionReason>();
		var release = candidate.Parsed;

		if (release.Protocol == Protocol.USENET && candidate.Info.PublishDate is { } published)
		{
			if (config.RetentionDays > 0 && (evaluatedAt - published).TotalDays > config.RetentionDays)
			{
				rejections.Add(new RejectionReason(
					$"release is below the retention of {config.RetentionDays} days",
					RejectionType.PERMANENT));
			}

			if (config.MinimumAgeMinutes > 0 && evaluatedAt < published.AddMinutes(config.MinimumAgeMinutes))
			{
				rejections.Add(new RejectionReason(
					$"waiting for minimum age of {config.MinimumAgeMinutes} minutes",
					RejectionType.TEMPORARY));
			}
		}

		if (config.MaximumSizeMb > 0 && candidate.Info.Size is { } size && size > (long)config.MaximumSizeMb * 1024 * 1024)
		{
			rejections.Add(new RejectionReason(
				$"size exceeds the maximum of {config.MaximumSizeMb} MB",
				RejectionType.PERMANENT));
		}

		return rejections;
	}

	// a candidate on a protocol the delay profile delays is held until its publish date clears the window, unless the
	// profile bypasses the delay for the highest quality or a high custom format score
	private static bool IsWithinDelayWindow(
		ReleaseCandidate candidate,
		DecisionContext context,
		int qualityIndex,
		int customFormatScore,
		DateTime now)
	{
		if (context.DelayProfile is not { } delay || candidate.Info.PublishDate is not { } published)
		{
			return false;
		}

		var delayMinutes = candidate.Parsed.Protocol switch
		{
			Protocol.BITTORRENT => delay.TorrentDelayMinutes,
			Protocol.USENET => delay.UsenetDelayMinutes,
			_ => 0
		};

		if (delayMinutes <= 0 || published.AddMinutes(delayMinutes) <= now)
		{
			return false;
		}

		if (delay.BypassIfHighestQuality && qualityIndex == context.QualityProfile.Items.FindLastIndex(item => item.Allowed))
		{
			return false;
		}

		return !(delay.BypassIfAboveCustomFormatScore && customFormatScore > delay.MinimumCustomFormatScore);
	}

	// a term matches as a case-insensitive substring, or as a regular expression when wrapped in /.../; an invalid
	// pattern or a runaway match never throws, it just never matches
	private static bool MatchesTerm(string title, string term)
	{
		if (term is not ['/', _, .., '/'])
		{
			return title.Contains(term, StringComparison.OrdinalIgnoreCase);
		}

		try
		{
			return System.Text.RegularExpressions.Regex.IsMatch(
				title,
				term[1..^1],
				System.Text.RegularExpressions.RegexOptions.IgnoreCase,
				TimeSpan.FromSeconds(1));
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
		{
			return false;
		}
	}

	private static int Score(ReleaseCandidate candidate, DecisionContext context, int qualityIndex, int filterScore,
		int customFormatScore)
	{
		var release = candidate.Parsed;

		var score = qualityIndex * QualityWeight
		            + release.Quality.Revision.Version * RevisionWeight
		            + filterScore
		            + customFormatScore;

		if (context.SeasonReleaseGroup is not null
		    && string.Equals(context.SeasonReleaseGroup, release.ReleaseGroup, StringComparison.OrdinalIgnoreCase))
		{
			score += ConsistentReleaseGroupBonus;
		}

		score -= candidate.Info.IndexerPriority;

		if (release.SeriesReleaseData?.ReleaseType == SeriesReleaseType.FULL_SEASON)
		{
			score += FullSeasonBonus;
		}

		if (context.DelayProfile is { } delay && release.Protocol == delay.PreferredProtocol)
		{
			score += PreferredProtocolBonus;
		}

		return score;
	}
}
