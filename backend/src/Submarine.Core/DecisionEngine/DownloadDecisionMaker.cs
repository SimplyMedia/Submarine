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

		if (IsSample(release.FullTitle, candidate.Info.Size))
		{
			rejections.Add(new RejectionReason("sample release", RejectionType.PERMANENT));
		}

		if (release.SeriesReleaseData?.ReleaseType == SeriesReleaseType.MULTI_SEASON)
		{
			rejections.Add(new RejectionReason("multi-season releases are not supported", RejectionType.PERMANENT));
		}

		if (context.ExistingFileCoversMoreEpisodes)
		{
			rejections.Add(new RejectionReason(
				"the episode file on disk contains more episodes than this release contains",
				RejectionType.PERMANENT));
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

			// a repack, proper or anime version bump replacing the held file at the same quality tier only counts as
			// a genuine upgrade when the release group matches: a different group is likely a different encode, not
			// a repack of the same source
			if (qualityIndex == existingIndex
			    && QualityProfileExtensions.IsRevisionUpgrade(existingQuality.Revision, release.Quality.Revision, context.DownloadPropersAndRepacks)
			    && (release.Quality.Revision.IsRepack || context.SeriesType == SeriesType.ANIME))
			{
				if (string.IsNullOrWhiteSpace(context.SeasonReleaseGroup) || string.IsNullOrWhiteSpace(release.ReleaseGroup))
				{
					rejections.Add(new RejectionReason(
						"cannot verify the repack/version release group, the existing file's or the release's release group is unknown",
						RejectionType.PERMANENT));
				}
				else if (!string.Equals(context.SeasonReleaseGroup, release.ReleaseGroup, StringComparison.OrdinalIgnoreCase))
				{
					rejections.Add(new RejectionReason(
						$"repack/version release group '{release.ReleaseGroup}' does not match the existing release group '{context.SeasonReleaseGroup}'",
						RejectionType.PERMANENT));
				}
			}
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

		if (candidate.Info.InfoHash is { } infoHash && context.AlreadyImportedInfoHashes.Contains(infoHash))
		{
			rejections.Add(new RejectionReason(
				"has the same info hash as a release already grabbed and imported",
				RejectionType.PERMANENT));
		}
		else if (context.AlreadyImportedTitles.Contains(release.FullTitle))
		{
			rejections.Add(new RejectionReason(
				"has the same title as a release already grabbed and imported",
				RejectionType.PERMANENT));
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

		if (context.AvailableFreeSpaceBytes is { } freeSpace)
		{
			var remaining = freeSpace - (candidate.Info.Size ?? 0);

			if (remaining <= 0)
			{
				rejections.Add(new RejectionReason(
					"importing after download would exceed the available disk space",
					RejectionType.PERMANENT));
			}
			else if (remaining < context.MinimumFreeSpaceMb * 1024L * 1024)
			{
				rejections.Add(new RejectionReason(
					$"not enough free space to import after download, minimum is {context.MinimumFreeSpaceMb} MB",
					RejectionType.PERMANENT));
			}
		}

		if (release.Protocol == Protocol.BITTORRENT
		    && candidate.MinimumSeeders is { } minimumSeeders
		    && candidate.Info.Seeders is { } seeders
		    && seeders < minimumSeeders)
		{
			rejections.Add(new RejectionReason($"{seeders} seeders, minimum is {minimumSeeders}", RejectionType.TEMPORARY));
		}

		if (release.Protocol == Protocol.BITTORRENT
		    && candidate.Info.IndexerId is { } requiredFlagsIndexerId
		    && context.IndexerRequiredFlags.TryGetValue(requiredFlagsIndexerId, out var requiredFlags)
		    && requiredFlags.Count > 0
		    && !requiredFlags.Any(flag => candidate.Info.IndexerFlags.Contains(flag)))
		{
			rejections.Add(new RejectionReason(
				$"none of the required indexer flags ({string.Join(", ", requiredFlags)}) were found",
				RejectionType.PERMANENT));
		}

		if (IsWithinDelayWindow(candidate, context, qualityIndex, customFormatScore, evaluatedAt))
		{
			rejections.Add(new RejectionReason("waiting for delay window", RejectionType.TEMPORARY));
		}

		if (context.DelayProfile is { } enabledProfile
		    && ((release.Protocol == Protocol.USENET && !enabledProfile.EnableUsenet)
		        || (release.Protocol == Protocol.BITTORRENT && !enabledProfile.EnableTorrent)))
		{
			rejections.Add(new RejectionReason($"{release.Protocol} is not enabled for this media", RejectionType.PERMANENT));
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
		var sizePreferenceKey = SizePreference(definition, context.RuntimeMinutes, candidate.Info.Size);

		return new DownloadDecision(candidate, approved, score, rejections, matchedFormats, customFormatScore, sizePreferenceKey);
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

		if (release.HardcodedSubs && !config.AllowHardcodedSubs)
		{
			var whitelistedGroups = config.WhitelistedHardcodedSubs
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			var isWhitelisted = release.ReleaseGroup is { } group
			                     && whitelistedGroups.Any(allowed => string.Equals(allowed, group, StringComparison.OrdinalIgnoreCase));

			if (!isWhitelisted)
			{
				rejections.Add(new RejectionReason("release reports hardcoded subtitles", RejectionType.PERMANENT));
			}
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

	// a title containing "sample" is only rejected when its reported size is small: an unknown size is not assumed
	// to be a sample, unlike Sonarr/Radarr's non-nullable release size which defaults to zero
	private static bool IsSample(string title, long? sizeBytes)
		=> title.Contains("sample", StringComparison.OrdinalIgnoreCase) && sizeBytes is { } bytes && bytes < 70L * 1024 * 1024;

	// final ordering tie-break, higher is preferred: closeness to the quality definition's preferred size when known,
	// bucketed to 200 MB like Sonarr/Radarr so near-identical releases do not reorder on tiny size differences;
	// otherwise larger releases sort first
	private static double SizePreference(QualityDefinition? definition, int? runtimeMinutes, long? sizeBytes)
	{
		const double bucketBytes = 200d * 1024 * 1024;
		var size = sizeBytes ?? 0;

		if (definition?.PreferredSizeMbPerMinute is { } preferredPerMinute && runtimeMinutes is > 0 and { } runtime)
		{
			var preferredBytes = preferredPerMinute * runtime * 1024d * 1024d;
			return -Math.Round(Math.Abs(size - preferredBytes) / bucketBytes) * bucketBytes;
		}

		return Math.Round(size / bucketBytes) * bucketBytes;
	}
}
