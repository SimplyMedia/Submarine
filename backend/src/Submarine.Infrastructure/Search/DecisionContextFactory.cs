using Microsoft.EntityFrameworkCore;
using Submarine.Core.CustomFormats;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Profiles;
using Submarine.Core.MediaFiles;
using Submarine.Core.Quality;
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
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task<DecisionContext> BuildAsync(
		MediaVersion version,
		IReadOnlyCollection<int>? episodeIds = null,
		CancellationToken cancellationToken = default)
	{
		var qualityProfile = await db.QualityProfiles.AsNoTracking()
			.FirstOrDefaultAsync(profile => profile.Id == version.QualityProfileId, cancellationToken) ?? new QualityProfile();
		var languageProfile = await db.LanguageProfiles.AsNoTracking()
			.FirstOrDefaultAsync(profile => profile.Id == version.LanguageProfileId, cancellationToken) ?? new LanguageProfile();

		SeriesType? seriesType = null;
		int? runtimeMinutes = null;
		bool? minimumAvailabilityMet = null;
		var tagIds = new List<int>();

		if (version.SeriesId is { } seriesId)
		{
			var series = await db.Series.AsNoTracking().Include(entity => entity.Tags)
				.FirstOrDefaultAsync(entity => entity.Id == seriesId, cancellationToken);
			seriesType = series?.Type;
			runtimeMinutes = series?.Runtime;
			tagIds = series?.Tags.Select(tag => tag.Id).ToList() ?? [];
		}
		else if (version.MovieId is { } movieId)
		{
			var movie = await db.Movies.AsNoTracking().Include(entity => entity.Tags)
				.FirstOrDefaultAsync(entity => entity.Id == movieId, cancellationToken);
			runtimeMinutes = movie?.Runtime;
			tagIds = movie?.Tags.Select(tag => tag.Id).ToList() ?? [];
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

		if (version.SeriesId is not null)
		{
			var filesQuery = db.EpisodeFiles.AsNoTracking().Where(file => file.MediaVersionId == version.Id);
			if (episodeIds is { Count: > 0 })
			{
				filesQuery = filesQuery.Where(file => file.Episodes.Any(episode => episodeIds.Contains(episode.Id)));
			}

			var files = await filesQuery.ToListAsync(cancellationToken);

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
			}
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
			QueuedReleases = queuedReleases
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
