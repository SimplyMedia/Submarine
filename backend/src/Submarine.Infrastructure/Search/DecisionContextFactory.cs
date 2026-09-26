using Microsoft.EntityFrameworkCore;
using Submarine.Core.CustomFormats;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Profiles;
using Submarine.Core.MediaFiles;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Builds the <see cref="DecisionContext" /> a media version's releases are decided against: profiles, the
///     currently held file, applicable delay/release/filter rules, custom formats and global indexer behaviour.
/// </summary>
/// <param name="db">The database context.</param>
/// <param name="blocklistService">Supplies the blocklist predicate.</param>
public sealed class DecisionContextFactory(SubmarineDbContext db, IBlocklistService blocklistService)
{
	/// <summary>
	///     Builds the decision context for one media version, scoped to a candidate's episode set when evaluating a
	///     multi-episode or season pack candidate.
	/// </summary>
	/// <param name="version">The media version.</param>
	/// <param name="episodeIds">
	///     Episode ids the candidate covers, when the version is a series. Determines both <see cref="DecisionContext.EpisodeCount" />
	///     and which held file(s) the candidate is compared against: only episodes in this set are considered, and the
	///     comparison uses the worst quality among them so a season pack still has to upgrade the weakest covered episode.
	/// </param>
	/// <param name="isInteractive">Whether this decision is for a user initiated interactive search.</param>
	/// <param name="isSeasonSearch">Whether this decision is for an interactive season-level search covering multiple episodes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task<DecisionContext> BuildAsync(
		MediaVersion version,
		IReadOnlyCollection<int>? episodeIds = null,
		bool isInteractive = false,
		bool isSeasonSearch = false,
		CancellationToken cancellationToken = default)
	{
		var qualityProfile = await db.QualityProfiles.AsNoTracking()
			.FirstOrDefaultAsync(profile => profile.Id == version.QualityProfileId, cancellationToken) ?? new QualityProfile();
		var languageProfile = await db.LanguageProfiles.AsNoTracking()
			.FirstOrDefaultAsync(profile => profile.Id == version.LanguageProfileId, cancellationToken) ?? new LanguageProfile();

		SeriesType? seriesType = null;
		int? runtimeMinutes = null;
		bool? minimumAvailabilityMet = null;
		var mediaMonitored = true;
		var tagIds = new List<int>();

		if (version.SeriesId is { } seriesId)
		{
			var series = await db.Series.AsNoTracking().Include(entity => entity.Tags)
				.FirstOrDefaultAsync(entity => entity.Id == seriesId, cancellationToken);
			seriesType = series?.Type;
			runtimeMinutes = series?.Runtime;
			tagIds = series?.Tags.Select(tag => tag.Id).ToList() ?? [];
			mediaMonitored = series?.Monitored ?? true;
		}
		else if (version.MovieId is { } movieId)
		{
			var movie = await db.Movies.AsNoTracking().Include(entity => entity.Tags)
				.FirstOrDefaultAsync(entity => entity.Id == movieId, cancellationToken);
			runtimeMinutes = movie?.Runtime;
			tagIds = movie?.Tags.Select(tag => tag.Id).ToList() ?? [];
			mediaMonitored = movie?.Monitored ?? true;
			minimumAvailabilityMet = movie is null ? null : IsAvailabilityMet(movie, DateTime.UtcNow);
		}

		var delayProfile = await db.DelayProfiles.Include(profile => profile.Tags).AsNoTracking()
			.Where(profile => profile.Tags.Count == 0 || profile.Tags.Any(tag => tagIds.Contains(tag.Id)))
			.OrderBy(profile => profile.Order)
			.FirstOrDefaultAsync(cancellationToken);

		var releaseProfiles = await db.ReleaseProfiles.Include(profile => profile.Tags).AsNoTracking()
			.Where(profile => profile.Enabled && (profile.Tags.Count == 0 || profile.Tags.Any(tag => tagIds.Contains(tag.Id))))
			.ToListAsync(cancellationToken);

		var releaseFilters = await db.ReleaseFilters.AsNoTracking().ToListAsync(cancellationToken);
		var customFormats = await db.CustomFormats.AsNoTracking().ToListAsync(cancellationToken);
		var qualityDefinitions = await db.QualityDefinitions.AsNoTracking().ToListAsync(cancellationToken);
		var indexerConfig = await db.IndexerConfig.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
		var mediaManagementConfig = await db.MediaManagementConfig.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

		QualityModel? existingQuality = null;
		IReadOnlyList<Language>? existingLanguages = null;
		var existingScore = 0;
		string? seasonReleaseGroup = null;
		DateTime? existingFileAddedDate = null;
		var existingFileCoversMoreEpisodes = false;
		int? monitoredEpisodeCount = null;
		int? daysSinceSeasonLastAired = null;

		if (version.SeriesId is not null)
		{
			var filesQuery = db.EpisodeFiles.AsNoTracking().Include(file => file.Episodes)
				.Where(file => file.MediaVersionId == version.Id);
			if (episodeIds is { Count: > 0 })
			{
				filesQuery = filesQuery.Where(file => file.Episodes.Any(episode => episodeIds.Contains(episode.Id)));
			}

			var files = await filesQuery.ToListAsync(cancellationToken);

			// an existing multi-episode file that reaches outside the candidate's episode set already covers more
			// than a narrower release would, so re-grabbing the narrower release would lose episodes on import
			existingFileCoversMoreEpisodes = episodeIds is { Count: > 0 }
				&& files.Any(file => file.Episodes.Any(episode => !episodeIds.Contains(episode.Id)));

			// a candidate covering episodes that are still missing a file must not be rejected because a different
			// episode already meets the cutoff: only compare when every requested episode has a file, against the
			// worst quality among them so the release still has to upgrade the weakest covered episode
			var coversEveryEpisode = episodeIds is not { Count: > 0 }
				|| episodeIds.All(episodeId => files.Any(file => file.Episodes.Any(episode => episode.Id == episodeId)));

			if (files.Count > 0 && coversEveryEpisode)
			{
				var worst = files.OrderBy(file => qualityProfile.GetIndex(file.Quality)).First();
				existingQuality = worst.Quality;
				existingLanguages = worst.Languages;
				existingScore = ScoreExistingFile(qualityProfile, worst.SceneName ?? worst.RelativePath, worst.Quality, worst.Languages, worst.ReleaseGroup, customFormats);
				seasonReleaseGroup = worst.ReleaseGroup;
				existingFileAddedDate = worst.DateAdded;
			}
			if (episodeIds is { Count: > 0 })
			{
				monitoredEpisodeCount = await db.Episodes.AsNoTracking()
					.Where(episode => episodeIds.Contains(episode.Id))
					.CountAsync(episode => episode.Monitored, cancellationToken);

				if (isSeasonSearch)
				{
					var seasonNumber = await db.Episodes.AsNoTracking()
						.Where(episode => episodeIds.Contains(episode.Id))
						.Select(episode => (int?)episode.SeasonNumber)
						.FirstOrDefaultAsync(cancellationToken);

					if (seasonNumber is { } season)
					{
						var lastAired = await db.Episodes.AsNoTracking()
							.Where(episode => episode.SeriesId == version.SeriesId && episode.SeasonNumber == season && episode.AirDateUtc != null)
							.MaxAsync(episode => (DateTime?)episode.AirDateUtc, cancellationToken);

						daysSinceSeasonLastAired = lastAired is { } aired ? (int)(DateTime.UtcNow - aired).TotalDays : null;
					}
				}
			}
		}
		else if (version.MovieId is not null)
		{
			var file = await db.MovieFiles.AsNoTracking().FirstOrDefaultAsync(file => file.MediaVersionId == version.Id, cancellationToken);
			if (file is not null)
			{
				existingQuality = file.Quality;
				existingLanguages = file.Languages;
				existingScore = ScoreExistingFile(qualityProfile, file.SceneName ?? file.RelativePath, file.Quality, file.Languages, file.ReleaseGroup, customFormats);
				seasonReleaseGroup = file.ReleaseGroup;
				existingFileAddedDate = file.DateAdded;
			}
		}

		long? availableFreeSpaceBytes = null;
		if (mediaManagementConfig?.SkipFreeSpaceCheck != true)
		{
			var rootFolder = await db.RootFolders.AsNoTracking()
				.FirstOrDefaultAsync(root => root.Id == version.RootFolderId, cancellationToken);
			if (rootFolder is not null)
			{
				try
				{
					var versionPath = MediaVersionPathGuard.IsSingleRelativeSegment(version.Path)
						? MediaVersionPathGuard.ResolveUnderRoot(rootFolder.Path, version.Path)
						: rootFolder.Path;
					availableFreeSpaceBytes = DiskSpace.Query(versionPath)?.FreeBytes;
				}
				catch (InvalidOperationException)
				{
					// the stored path escapes its root folder, skip the check rather than fail the whole decision
				}
			}
		}

		var alreadyImportedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var alreadyImportedInfoHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var downloadConfig = await db.DownloadConfig.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

		if (downloadConfig?.EnableCompletedDownloadHandling == true && existingQuality is not null)
		{
			var historyQuery = db.HistoryEvents.AsNoTracking()
				.Where(entry => entry.Type == HistoryEventType.GRABBED || entry.Type == HistoryEventType.IMPORTED);

			historyQuery = version.SeriesId is not null
				? episodeIds is { Count: > 0 }
					? historyQuery.Where(entry => entry.EpisodeId != null && episodeIds.Contains(entry.EpisodeId.Value))
					: historyQuery.Where(entry => false)
				: historyQuery.Where(entry => entry.MovieId == version.MovieId);

			var history = await historyQuery.ToListAsync(cancellationToken);
			var importedEvents = history.Where(entry => entry.Type == HistoryEventType.IMPORTED).ToList();

			// a grab followed by an import of a different quality means the release under-delivered: hold its title
			// and download id back so the same release is not grabbed again for the same episode or movie
			foreach (var grabbed in history.Where(entry => entry.Type == HistoryEventType.GRABBED))
			{
				var imported = importedEvents.FirstOrDefault(entry => entry.DownloadId == grabbed.DownloadId);
				if (imported is null || Equals(imported.Quality, grabbed.Quality))
				{
					continue;
				}

				alreadyImportedTitles.Add(grabbed.SourceTitle);
				if (grabbed.DownloadId is { } downloadId)
				{
					alreadyImportedInfoHashes.Add(downloadId);
				}
			}
		}

		// RequiredFlags is stored as JSON, so filter the (few) indexers in memory.
		var indexers = await db.Indexers.AsNoTracking().Include(indexer => indexer.Tags).ToListAsync(cancellationToken);
		var indexerRequiredFlags = indexers
			.Where(indexer => indexer.RequiredFlags.Count > 0)
			.ToDictionary(indexer => indexer.Id, indexer => (IReadOnlyList<IndexerFlag>)indexer.RequiredFlags);
		var indexerTagIds = indexers
			.Where(indexer => indexer.Tags.Count > 0)
			.ToDictionary(indexer => indexer.Id, indexer => (IReadOnlyList<int>)indexer.Tags.Select(tag => tag.Id).ToList());
		var indexerSeasonSearchMaxAge = indexers
			.Where(indexer => indexer.SeasonSearchMaximumSingleEpisodeAge > 0)
			.ToDictionary(indexer => indexer.Id, indexer => indexer.SeasonSearchMaximumSingleEpisodeAge);

		QualityModel? recentGrabQuality = null;
		var recentGrabCustomFormatScore = 0;
		var recentGrabQuery = db.HistoryEvents.AsNoTracking()
			.Where(entry => entry.Type == HistoryEventType.GRABBED && entry.Date > DateTime.UtcNow.AddHours(-12));

		recentGrabQuery = version.SeriesId is not null
			? episodeIds is { Count: > 0 }
				? recentGrabQuery.Where(entry => entry.EpisodeId != null && episodeIds.Contains(entry.EpisodeId.Value))
				: recentGrabQuery.Where(entry => false)
			: recentGrabQuery.Where(entry => entry.MovieId == version.MovieId);

		var recentGrab = await recentGrabQuery.OrderByDescending(entry => entry.Date).FirstOrDefaultAsync(cancellationToken);
		if (recentGrab?.Quality is { } grabbedQuality)
		{
			recentGrabQuality = grabbedQuality;
			recentGrabCustomFormatScore = ScoreExistingFile(
				qualityProfile, recentGrab.SourceTitle, grabbedQuality, recentGrab.Languages ?? [], null, customFormats);
		}

		var queuedStates = new[] { TrackedDownloadState.DOWNLOADING, TrackedDownloadState.IMPORT_PENDING, TrackedDownloadState.IMPORTING };
		var tracked = await db.TrackedDownloads.AsNoTracking()
			.Where(download => download.MediaVersionId == version.Id && download.Quality != null && queuedStates.Contains(download.State))
			.ToListAsync(cancellationToken);

		var queuedReleases = tracked
			.Select(download => new QueuedRelease(
				download.EpisodeIds,
				download.MovieId,
				download.Quality!,
				ScoreExistingFile(qualityProfile, download.ReleaseTitle ?? download.Title, download.Quality!, download.Languages, download.ReleaseGroup, customFormats)))
			.ToList();

		var isBlocklisted = await blocklistService.BuildPredicateAsync(cancellationToken);

		return new DecisionContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = languageProfile,
			ExistingFileQuality = existingQuality,
			ExistingFileLanguages = existingLanguages,
			ExistingCustomFormatScore = existingScore,
			SeasonReleaseGroup = seasonReleaseGroup,
			DelayProfile = delayProfile,
			ReleaseProfiles = releaseProfiles,
			ReleaseFilters = releaseFilters,
			CustomFormats = customFormats,
			QualityDefinitions = qualityDefinitions,
			IndexerConfig = indexerConfig,
			MinimumAvailabilityMet = minimumAvailabilityMet,
			SeriesType = seriesType,
			RuntimeMinutes = runtimeMinutes,
			EpisodeCount = Math.Max(episodeIds?.Count ?? 1, 1),
			DownloadPropersAndRepacks = mediaManagementConfig?.DownloadPropersAndRepacks ?? DownloadPropersAndRepacks.PREFER_AND_UPGRADE,
			IsBlocklisted = isBlocklisted,
			QueuedReleases = queuedReleases,
			ExistingFileCoversMoreEpisodes = existingFileCoversMoreEpisodes,
			AvailableFreeSpaceBytes = availableFreeSpaceBytes,
			MinimumFreeSpaceMb = mediaManagementConfig?.MinimumFreeSpaceMb ?? 100,
			AlreadyImportedTitles = alreadyImportedTitles,
			AlreadyImportedInfoHashes = alreadyImportedInfoHashes,
			IndexerRequiredFlags = indexerRequiredFlags,
			IsInteractive = isInteractive,
			MediaMonitored = mediaMonitored,
			MonitoredEpisodeCount = monitoredEpisodeCount,
			MediaTagIds = tagIds,
			IndexerTagIds = indexerTagIds,
			RecentGrabQuality = recentGrabQuality,
			RecentGrabCustomFormatScore = recentGrabCustomFormatScore,
			ExistingFileAddedDate = existingFileAddedDate,
			IsSeasonSearch = isSeasonSearch,
			DaysSinceSeasonLastAired = daysSinceSeasonLastAired,
			IndexerSeasonSearchMaxAge = indexerSeasonSearchMaxAge
		};
	}

	private static int ScoreExistingFile(
		QualityProfile qualityProfile,
		string title,
		QualityModel quality,
		IReadOnlyList<Language> languages,
		string? releaseGroup,
		IReadOnlyCollection<CustomFormat> customFormats)
	{
		var release = new Core.Release.BaseRelease
		{
			FullTitle = title,
			Title = title,
			Languages = languages,
			Quality = quality,
			ReleaseGroup = releaseGroup
		};
		var context = new ReleaseContext(title, release);
		var matched = CustomFormatCalculator.Match(customFormats, context);
		return CustomFormatCalculator.Score(qualityProfile, matched);
	}

	private static bool IsAvailabilityMet(Movie movie, DateTime now)
		=> movie.MinimumAvailability switch
		{
			MinimumAvailability.ANNOUNCED => true,
			MinimumAvailability.IN_CINEMAS => movie.Status != MovieStatus.ANNOUNCED || (movie.InCinemasDate is { } inCinemas && inCinemas <= now),
			MinimumAvailability.RELEASED => movie.Status == MovieStatus.RELEASED
				|| (movie.DigitalReleaseDate is { } digital && digital <= now)
				|| (movie.PhysicalReleaseDate is { } physical && physical <= now),
			_ => true
		};
}
