using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.CustomFormats;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Languages;
using Submarine.Core.MediaFile;
using Submarine.Core.MediaFiles;
using Submarine.Core.Naming;
using Submarine.Core.Parser;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Infrastructure.MediaFiles;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Import;

/// <inheritdoc cref="IImportService" />
public sealed class ImportService(
	SubmarineDbContext db,
	IParser<BaseRelease> releaseParser,
	NamingService namingService,
	IFileLinker fileLinker,
	IRecycleBinService recycleBinService,
	IMetadataConsumerWriter metadataWriter,
	IFileDateService fileDateService,
	IMediaInfoService mediaInfoService,
	IEventBus eventBus,
	TimeProvider timeProvider,
	ILogger<ImportService> logger) : IImportService
{
	/// <inheritdoc />
	public async Task<ImportRunSummary> ImportTrackedDownloadAsync(int trackedDownloadId, CancellationToken cancellationToken = default)
	{
		var download = await db.TrackedDownloads.FirstOrDefaultAsync(x => x.Id == trackedDownloadId, cancellationToken)
			?? throw new KeyNotFoundException($"Tracked download {trackedDownloadId} not found");

		if (download.MediaVersionId is null || download.SeriesId is null && download.MovieId is null || string.IsNullOrWhiteSpace(download.OutputPath))
		{
			await FailImportAsync(download, "Download has no matched series/movie, version or output path", cancellationToken);
			return new ImportRunSummary([]);
		}

		try
		{
			var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
			var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
			var version = await db.MediaVersions.FirstOrDefaultAsync(x => x.Id == download.MediaVersionId, cancellationToken)
				?? throw new KeyNotFoundException($"Version {download.MediaVersionId} not found");
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken)
				?? throw new KeyNotFoundException($"Root folder {version.RootFolderId} not found");
			var qualityProfile = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == version.QualityProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Quality profile {version.QualityProfileId} not found");
			var languageProfile = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == version.LanguageProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Language profile {version.LanguageProfileId} not found");
			var customFormats = await db.CustomFormats.AsNoTracking().ToListAsync(cancellationToken);

			var files = EnumerateCandidateFiles(download.OutputPath);
			var outcomes = new List<ImportFileOutcome>();
			var claimedEpisodeIds = new HashSet<int>();

			foreach (var file in files)
			{
				ImportFileOutcome outcome;
				if (download.SeriesId is { } seriesId)
				{
					var series = await db.Series.FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken)
						?? throw new KeyNotFoundException($"Series {seriesId} not found");
					outcome = await ImportEpisodeFileAsync(
						file, series, version, root, qualityProfile, languageProfile, customFormats, naming, mediaManagement,
						download.EpisodeIds, claimedEpisodeIds, forceImport: false, ImportMode.MOVE, download.DownloadId, download.ReleaseTitle ?? download.Title,
						null, null, null, cancellationToken);
				}
				else
				{
					var movie = await db.Movies.FirstOrDefaultAsync(x => x.Id == download.MovieId, cancellationToken)
						?? throw new KeyNotFoundException($"Movie {download.MovieId} not found");
					outcome = await ImportMovieFileAsync(
						file, movie, version, root, qualityProfile, languageProfile, customFormats, naming, mediaManagement,
						forceImport: false, ImportMode.MOVE, download.DownloadId, download.ReleaseTitle ?? download.Title,
						null, null, null, cancellationToken);
				}

				outcomes.Add(outcome);
			}

			await UpdateDownloadStateAsync(download, outcomes, cancellationToken);

			if (outcomes.Any(x => x.Imported) && mediaManagement.DeleteEmptyFolders && Directory.Exists(download.OutputPath))
			{
				DeleteIfEmpty(download.OutputPath);
			}

			await eventBus.PublishAsync(new QueueUpdatedEvent(), cancellationToken);
			return new ImportRunSummary(outcomes);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogError(exception, "Import failed for tracked download {DownloadId}", download.DownloadId);
			await FailImportAsync(download, $"Import failed: {exception.Message}", cancellationToken);
			return new ImportRunSummary([]);
		}
	}

	private async Task FailImportAsync(TrackedDownload download, string message, CancellationToken cancellationToken)
	{
		download.State = TrackedDownloadState.FAILED_PENDING;
		download.StatusMessages = [message];
		await db.SaveChangesAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ImportRunSummary> ImportManualAsync(IReadOnlyList<ManualImportSelection> selections, ImportMode mode, CancellationToken cancellationToken = default)
	{
		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var outcomes = new List<ImportFileOutcome>();
		var touchedDownloadIds = new HashSet<string>();

		foreach (var selection in selections)
		{
			var version = await db.MediaVersions.FirstOrDefaultAsync(x => x.Id == selection.MediaVersionId, cancellationToken)
				?? throw new KeyNotFoundException($"Version {selection.MediaVersionId} not found");
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken)
				?? throw new KeyNotFoundException($"Root folder {version.RootFolderId} not found");
			var qualityProfile = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == version.QualityProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Quality profile {version.QualityProfileId} not found");
			var languageProfile = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == version.LanguageProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Language profile {version.LanguageProfileId} not found");
			var customFormats = await db.CustomFormats.AsNoTracking().ToListAsync(cancellationToken);

			ImportFileOutcome outcome;
			if (selection.SeriesId is { } seriesId)
			{
				var series = await db.Series.FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken)
					?? throw new KeyNotFoundException($"Series {seriesId} not found");
				outcome = await ImportEpisodeFileAsync(
					selection.Path, series, version, root, qualityProfile, languageProfile, customFormats, naming, mediaManagement,
					selection.EpisodeIds ?? [], null, forceImport: true, mode, selection.DownloadId, null,
					selection.Quality, selection.Languages, selection.ReleaseGroup, cancellationToken);
			}
			else if (selection.MovieId is { } movieId)
			{
				var movie = await db.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
					?? throw new KeyNotFoundException($"Movie {movieId} not found");
				outcome = await ImportMovieFileAsync(
					selection.Path, movie, version, root, qualityProfile, languageProfile, customFormats, naming, mediaManagement,
					forceImport: true, mode, selection.DownloadId, null,
					selection.Quality, selection.Languages, selection.ReleaseGroup, cancellationToken);
			}
			else
			{
				outcome = new ImportFileOutcome(selection.Path, false, null, false, ImportRejectionReason.NO_MEDIA_MATCH, "Neither seriesId nor movieId was provided");
			}

			outcomes.Add(outcome);
			if (outcome.Imported && selection.DownloadId is not null)
			{
				touchedDownloadIds.Add(selection.DownloadId);
			}
		}

		if (touchedDownloadIds.Count > 0)
		{
			var downloads = await db.TrackedDownloads.Where(x => touchedDownloadIds.Contains(x.DownloadId)).ToListAsync(cancellationToken);
			foreach (var download in downloads)
			{
				download.State = TrackedDownloadState.IMPORTED;
				download.Imported = true;
			}

			await db.SaveChangesAsync(cancellationToken);
		}

		await eventBus.PublishAsync(new QueueUpdatedEvent(), cancellationToken);
		return new ImportRunSummary(outcomes);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ManualImportCandidate>> AnalyzeAsync(
		string path,
		int? seriesId,
		int? movieId,
		string? downloadId,
		CancellationToken cancellationToken = default)
	{
		var files = EnumerateCandidateFiles(path);
		var result = new List<ManualImportCandidate>();

		var series = seriesId is { } sid ? await db.Series.FirstOrDefaultAsync(x => x.Id == sid, cancellationToken) : null;
		var movie = movieId is { } mid ? await db.Movies.FirstOrDefaultAsync(x => x.Id == mid, cancellationToken) : null;
		var downloadTitle = downloadId is null
			? null
			: (await db.TrackedDownloads.FirstOrDefaultAsync(x => x.DownloadId == downloadId, cancellationToken))?.ReleaseTitle;

		foreach (var file in files)
		{
			var parsed = ParseWithFallback(file, downloadTitle);
			var size = SafeLength(file);

			if (series is not null)
			{
				var episodes = await ResolveEpisodesAsync(series, [], parsed, Path.GetFileNameWithoutExtension(file), cancellationToken);
				result.Add(new ManualImportCandidate(
					file, size, series.Id, [.. episodes.Select(x => x.Id)], null, null,
					parsed.Quality, parsed.Languages, parsed.ReleaseGroup,
					episodes.Count == 0 ? ImportRejectionReason.NO_MEDIA_MATCH : null));
			}
			else if (movie is not null)
			{
				result.Add(new ManualImportCandidate(file, size, null, [], movie.Id, null, parsed.Quality, parsed.Languages, parsed.ReleaseGroup, null));
			}
			else
			{
				result.Add(new ManualImportCandidate(file, size, null, [], null, null, parsed.Quality, parsed.Languages, parsed.ReleaseGroup, ImportRejectionReason.NO_MEDIA_MATCH));
			}
		}

		return result;
	}

	/// <inheritdoc />
	public async Task<ImportRunSummary> RescanSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
	{
		var series = await db.Series.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {seriesId} not found");
		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var outcomes = new List<ImportFileOutcome>();

		foreach (var version in series.Versions)
		{
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
			if (root is null)
			{
				continue;
			}

			string versionFolder;
			try
			{
				versionFolder = MediaVersionPathGuard.ResolveUnderRoot(root.Path, version.Path);
			}
			catch (InvalidOperationException exception)
			{
				logger.LogWarning(exception, "Skipping rescan of version {VersionId}: {Message}", version.Id, exception.Message);
				continue;
			}

			if (!Directory.Exists(versionFolder))
			{
				continue;
			}

			var qualityProfile = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == version.QualityProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Quality profile {version.QualityProfileId} not found");
			var languageProfile = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == version.LanguageProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Language profile {version.LanguageProfileId} not found");
			var customFormats = await db.CustomFormats.AsNoTracking().ToListAsync(cancellationToken);

			var registered = await db.EpisodeFiles.Include(x => x.Episodes).Where(x => x.MediaVersionId == version.Id).ToListAsync(cancellationToken);
			var onDisk = Directory.EnumerateFiles(versionFolder, "*", SearchOption.AllDirectories)
				.Select(Path.GetFullPath)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			if (onDisk.Count == 0)
			{
				// An empty enumeration more likely means an unmounted or not-yet-ready folder than a genuine
				// wipe of every file; never delete rows or unmonitor episodes on that assumption.
				continue;
			}

			foreach (var file in registered)
			{
				var fullPath = Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath));
				if (onDisk.Contains(fullPath))
				{
					continue;
				}

				var episodeIds = file.Episodes.Select(x => x.Id).ToList();
				db.EpisodeFiles.Remove(file);
				if (mediaManagement.UnmonitorDeletedFiles && episodeIds.Count > 0)
				{
					var episodes = await db.Episodes.Where(x => episodeIds.Contains(x.Id)).ToListAsync(cancellationToken);
					foreach (var episode in episodes)
					{
						episode.Monitored = false;
					}
				}

				await db.SaveChangesAsync(cancellationToken);
				await eventBus.PublishAsync(new EpisodeFileDeletedEvent(series.Id, version.Id, episodeIds, fullPath, FileDeleteReason.MISSING_FROM_DISK), cancellationToken);
				await WriteHistoryAsync(HistoryEventType.DELETED, series.Id, episodeIds.FirstOrDefault(), null, version.Id, file.SceneName ?? file.RelativePath, file.Quality, file.Languages, null, null, cancellationToken);
			}

			var registeredPaths = registered
				.Where(x => onDisk.Contains(Path.GetFullPath(Path.Combine(versionFolder, x.RelativePath))))
				.Select(x => Path.GetFullPath(Path.Combine(versionFolder, x.RelativePath)))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var file in registered)
			{
				var episode = file.Episodes.FirstOrDefault();
				if (episode is null || !registeredPaths.Contains(Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath))))
				{
					continue;
				}

				fileDateService.ApplyEpisodeFileDate(Path.Combine(versionFolder, file.RelativePath), mediaManagement.FileDate, episode);
			}

			var candidates = Directory.EnumerateFiles(versionFolder, "*", SearchOption.AllDirectories)
				.Where(x => MediaFileConstants.MediaFileExtensions.Contains(Path.GetExtension(x).ToLowerInvariant()))
				.Where(x => !registeredPaths.Contains(Path.GetFullPath(x)))
				.ToList();

			foreach (var candidate in candidates)
			{
				var outcome = await ImportEpisodeFileAsync(
					candidate, series, version, root, qualityProfile, languageProfile, customFormats, naming, mediaManagement,
					[], null, forceImport: true, ImportMode.ADOPT, null, null, null, null, null, cancellationToken);
				outcomes.Add(outcome);
			}
		}

		return new ImportRunSummary(outcomes);
	}

	/// <inheritdoc />
	public async Task<ImportRunSummary> RescanMovieAsync(int movieId, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {movieId} not found");
		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var outcomes = new List<ImportFileOutcome>();

		foreach (var version in movie.Versions)
		{
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
			if (root is null)
			{
				continue;
			}

			string versionFolder;
			try
			{
				versionFolder = MediaVersionPathGuard.ResolveUnderRoot(root.Path, version.Path);
			}
			catch (InvalidOperationException exception)
			{
				logger.LogWarning(exception, "Skipping rescan of version {VersionId}: {Message}", version.Id, exception.Message);
				continue;
			}

			if (!Directory.Exists(versionFolder))
			{
				continue;
			}

			var qualityProfile = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == version.QualityProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Quality profile {version.QualityProfileId} not found");
			var languageProfile = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == version.LanguageProfileId, cancellationToken)
				?? throw new KeyNotFoundException($"Language profile {version.LanguageProfileId} not found");
			var customFormats = await db.CustomFormats.AsNoTracking().ToListAsync(cancellationToken);

			var registered = await db.MovieFiles.Where(x => x.MediaVersionId == version.Id).ToListAsync(cancellationToken);
			var onDisk = Directory.EnumerateFiles(versionFolder, "*", SearchOption.AllDirectories)
				.Select(Path.GetFullPath)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			if (onDisk.Count == 0)
			{
				// An empty enumeration more likely means an unmounted or not-yet-ready folder than a genuine
				// wipe of every file; never delete rows on that assumption.
				continue;
			}

			foreach (var file in registered)
			{
				var fullPath = Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath));
				if (onDisk.Contains(fullPath))
				{
					continue;
				}

				db.MovieFiles.Remove(file);
				if (mediaManagement.UnmonitorDeletedFiles)
				{
					movie.Monitored = false;
				}

				await db.SaveChangesAsync(cancellationToken);
				await eventBus.PublishAsync(new MovieFileDeletedEvent(movie.Id, version.Id, fullPath, FileDeleteReason.MISSING_FROM_DISK), cancellationToken);
				await WriteHistoryAsync(HistoryEventType.DELETED, null, null, movie.Id, version.Id, file.SceneName ?? file.RelativePath, file.Quality, file.Languages, null, null, cancellationToken);
			}

			var registeredPaths = registered
				.Where(x => onDisk.Contains(Path.GetFullPath(Path.Combine(versionFolder, x.RelativePath))))
				.Select(x => Path.GetFullPath(Path.Combine(versionFolder, x.RelativePath)))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var file in registered)
			{
				if (registeredPaths.Contains(Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath))))
				{
					fileDateService.ApplyMovieFileDate(Path.Combine(versionFolder, file.RelativePath), mediaManagement.FileDate, movie);
				}
			}

			var candidates = Directory.EnumerateFiles(versionFolder, "*", SearchOption.AllDirectories)
				.Where(x => MediaFileConstants.MediaFileExtensions.Contains(Path.GetExtension(x).ToLowerInvariant()))
				.Where(x => !registeredPaths.Contains(Path.GetFullPath(x)))
				.ToList();

			foreach (var candidate in candidates)
			{
				var outcome = await ImportMovieFileAsync(
					candidate, movie, version, root, qualityProfile, languageProfile, customFormats, naming, mediaManagement,
					forceImport: true, ImportMode.ADOPT, null, null, null, null, null, cancellationToken);
				outcomes.Add(outcome);
			}
		}

		return new ImportRunSummary(outcomes);
	}

	private async Task<ImportFileOutcome> ImportEpisodeFileAsync(
		string sourcePath,
		Series series,
		MediaVersion version,
		RootFolder root,
		QualityProfile qualityProfile,
		LanguageProfile languageProfile,
		IReadOnlyList<CustomFormat> customFormats,
		NamingConfig naming,
		MediaManagementConfig mediaManagement,
		IReadOnlyList<int> knownEpisodeIds,
		HashSet<int>? claimedEpisodeIds,
		bool forceImport,
		ImportMode mode,
		string? downloadId,
		string? downloadTitle,
		QualityModel? qualityOverride,
		IReadOnlyList<Language>? languagesOverride,
		string? releaseGroupOverride,
		CancellationToken cancellationToken)
	{
		if (!File.Exists(sourcePath))
		{
			return new ImportFileOutcome(sourcePath, false, null, false, ImportRejectionReason.UNREADABLE, "File not found");
		}

		var parsed = ParseWithFallback(sourcePath, downloadTitle);
		var episodes = await ResolveEpisodesAsync(series, knownEpisodeIds, parsed, Path.GetFileNameWithoutExtension(sourcePath), cancellationToken);
		if (episodes.Count == 0)
		{
			return new ImportFileOutcome(sourcePath, false, null, false, ImportRejectionReason.NO_MEDIA_MATCH, "No matching episode found");
		}

		var size = SafeLength(sourcePath);
		var candidateQuality = qualityOverride ?? parsed.Quality;
		var candidateLanguages = languagesOverride ?? parsed.Languages;
		var candidateReleaseGroup = releaseGroupOverride ?? parsed.ReleaseGroup;
		var episodeIds = episodes.Select(x => x.Id).ToList();

		if (claimedEpisodeIds is not null && episodeIds.Count > 0 && episodeIds.All(claimedEpisodeIds.Contains))
		{
			return new ImportFileOutcome(sourcePath, false, null, false, ImportRejectionReason.NO_MEDIA_MATCH, "Episode already imported from this download");
		}

		var existingFiles = await db.EpisodeFiles
			.Include(x => x.Episodes)
			.Where(x => x.MediaVersionId == version.Id && x.Episodes.Any(e => episodeIds.Contains(e.Id)))
			.ToListAsync(cancellationToken);
		var existing = existingFiles.FirstOrDefault();

		var decision = forceImport
			? new ImportQualityDecision(true, existing is not null, null)
			: EvaluateQuality(qualityProfile, languageProfile, customFormats, mediaManagement, parsed, size, candidateQuality, candidateLanguages, existing?.Quality, existing?.Languages, existing?.SceneName, existing?.RelativePath, existing?.ReleaseGroup, existing?.Size);

		if (!decision.ShouldImport)
		{
			return new ImportFileOutcome(sourcePath, false, null, false, decision.Rejection, $"Rejected: {decision.Rejection}");
		}

		var mediaInfo = mediaManagement.EnableMediaInfo ? await mediaInfoService.ProbeAsync(sourcePath, cancellationToken) : null;
		var versionFolder = MediaVersionPathGuard.ResolveUnderRoot(root.Path, version.Path);
		string destinationPath;

		if (mode == ImportMode.ADOPT)
		{
			destinationPath = sourcePath;
		}
		else
		{
			var extension = Path.GetExtension(sourcePath);
			var targetFolder = series.SeasonFolder
				? MediaVersionPathGuard.ResolveUnderRoot(versionFolder, namingService.RenderSeasonFolder(naming, series, episodes[0].SeasonNumber))
				: versionFolder;

			var fileStub = new EpisodeFile
			{
				RelativePath = Path.GetFileName(sourcePath),
				Quality = candidateQuality,
				Languages = [.. candidateLanguages],
				ReleaseGroup = candidateReleaseGroup,
				MediaInfo = mediaInfo,
				SceneName = parsed.FullTitle.Length > 0 ? parsed.FullTitle : null
			};

			var fileName = naming.RenameEpisodes
				? namingService.RenderEpisodeFileName(series, episodes, fileStub, naming)
				: Path.GetFileNameWithoutExtension(sourcePath);

			destinationPath = Path.Combine(targetFolder, fileName + extension);

			var placedPath = PlaceNewFile(sourcePath, destinationPath, mode, mediaManagement.UseHardlinks);

			foreach (var oldFile in existingFiles)
			{
				var oldFullPath = Path.GetFullPath(Path.Combine(versionFolder, oldFile.RelativePath));
				if (PathsEqual(oldFullPath, sourcePath))
				{
					continue;
				}

				recycleBinService.Recycle(oldFullPath, root.Path, mediaManagement.RecycleBinPath, timeProvider);
			}

			destinationPath = FinalizePlacement(placedPath, destinationPath);

			fileLinker.ApplyPermissions(destinationPath, mediaManagement.ChmodFile, isDirectory: false);
			fileLinker.ApplyPermissions(targetFolder, mediaManagement.ChmodFolder, isDirectory: true);

			if (mediaManagement.ImportExtraFiles)
			{
				ImportExtraFiles(sourcePath, destinationPath, mediaManagement.ExtraFileExtensions, mediaManagement.UseHardlinks);
			}

		}

		var isUpgrade = existing is not null;
		var newFile = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = Path.GetRelativePath(versionFolder, destinationPath),
			Size = SafeLength(destinationPath),
			DateAdded = timeProvider.GetUtcNow().UtcDateTime,
			Quality = candidateQuality,
			Languages = [.. candidateLanguages],
			ReleaseGroup = candidateReleaseGroup,
			SceneName = parsed.FullTitle.Length > 0 ? parsed.FullTitle : downloadTitle,
			MediaInfo = mediaInfo
		};

		foreach (var episode in episodes)
		{
			newFile.Episodes.Add(episode);
		}

		newFile.NamedFromPlaceholder = namingService.UsesPlaceholderTitle(series, [.. episodes], newFile, naming);

		foreach (var oldFile in existingFiles)
		{
			db.EpisodeFiles.Remove(oldFile);
		}

		db.EpisodeFiles.Add(newFile);
		await db.SaveChangesAsync(cancellationToken);

		fileDateService.ApplyEpisodeFileDate(destinationPath, mediaManagement.FileDate, episodes[0]);
		await metadataWriter.WriteEpisodeAsync(series, version.Id, versionFolder, newFile, episodes, cancellationToken);

		foreach (var oldFile in existingFiles)
		{
			await eventBus.PublishAsync(
				new EpisodeFileDeletedEvent(series.Id, version.Id, [.. oldFile.Episodes.Select(x => x.Id)], Path.Combine(versionFolder, oldFile.RelativePath), FileDeleteReason.UPGRADE),
				cancellationToken);
			await WriteHistoryAsync(HistoryEventType.DELETED, series.Id, oldFile.Episodes.FirstOrDefault()?.Id, null, version.Id, oldFile.SceneName ?? oldFile.RelativePath, oldFile.Quality, oldFile.Languages, downloadId, null, cancellationToken);
		}

		await eventBus.PublishAsync(
			new EpisodeFileImportedEvent(series.Id, version.Id, newFile.Id, episodeIds, destinationPath, parsed.FullTitle, downloadId, isUpgrade, candidateQuality, candidateLanguages, candidateReleaseGroup),
			cancellationToken);
		await WriteHistoryAsync(HistoryEventType.IMPORTED, series.Id, episodeIds.FirstOrDefault(), null, version.Id, downloadTitle ?? parsed.FullTitle, candidateQuality, candidateLanguages, downloadId, JsonSerializer.Serialize(new { isUpgrade }), cancellationToken);
		claimedEpisodeIds?.UnionWith(episodeIds);

		return new ImportFileOutcome(sourcePath, true, destinationPath, isUpgrade, null, "Imported");
	}

	private async Task<ImportFileOutcome> ImportMovieFileAsync(
		string sourcePath,
		Movie movie,
		MediaVersion version,
		RootFolder root,
		QualityProfile qualityProfile,
		LanguageProfile languageProfile,
		IReadOnlyList<CustomFormat> customFormats,
		NamingConfig naming,
		MediaManagementConfig mediaManagement,
		bool forceImport,
		ImportMode mode,
		string? downloadId,
		string? downloadTitle,
		QualityModel? qualityOverride,
		IReadOnlyList<Language>? languagesOverride,
		string? releaseGroupOverride,
		CancellationToken cancellationToken)
	{
		if (!File.Exists(sourcePath))
		{
			return new ImportFileOutcome(sourcePath, false, null, false, ImportRejectionReason.UNREADABLE, "File not found");
		}

		var parsed = ParseWithFallback(sourcePath, downloadTitle);
		var size = SafeLength(sourcePath);
		var candidateQuality = qualityOverride ?? parsed.Quality;
		var candidateLanguages = languagesOverride ?? parsed.Languages;
		var candidateReleaseGroup = releaseGroupOverride ?? parsed.ReleaseGroup;

		var existingFiles = await db.MovieFiles.Where(x => x.MediaVersionId == version.Id).ToListAsync(cancellationToken);
		var existing = existingFiles.FirstOrDefault();

		var decision = forceImport
			? new ImportQualityDecision(true, existing is not null, null)
			: EvaluateQuality(qualityProfile, languageProfile, customFormats, mediaManagement, parsed, size, candidateQuality, candidateLanguages, existing?.Quality, existing?.Languages, existing?.SceneName, existing?.RelativePath, existing?.ReleaseGroup, existing?.Size);

		if (!decision.ShouldImport)
		{
			return new ImportFileOutcome(sourcePath, false, null, false, decision.Rejection, $"Rejected: {decision.Rejection}");
		}

		var mediaInfo = mediaManagement.EnableMediaInfo ? await mediaInfoService.ProbeAsync(sourcePath, cancellationToken) : null;
		var versionFolder = MediaVersionPathGuard.ResolveUnderRoot(root.Path, version.Path);
		string destinationPath;

		if (mode == ImportMode.ADOPT)
		{
			destinationPath = sourcePath;
		}
		else
		{
			var extension = Path.GetExtension(sourcePath);
			var fileStub = new MovieFile
			{
				RelativePath = Path.GetFileName(sourcePath),
				Quality = candidateQuality,
				Languages = [.. candidateLanguages],
				ReleaseGroup = candidateReleaseGroup,
				Edition = parsed.MovieReleaseData?.Edition,
				MediaInfo = mediaInfo,
				SceneName = parsed.FullTitle.Length > 0 ? parsed.FullTitle : null
			};

			var fileName = naming.RenameMovies
				? namingService.RenderMovieFileName(movie, fileStub, naming)
				: Path.GetFileNameWithoutExtension(sourcePath);

			destinationPath = Path.Combine(versionFolder, fileName + extension);

			var placedPath = PlaceNewFile(sourcePath, destinationPath, mode, mediaManagement.UseHardlinks);

			foreach (var oldFile in existingFiles)
			{
				var oldFullPath = Path.GetFullPath(Path.Combine(versionFolder, oldFile.RelativePath));
				if (PathsEqual(oldFullPath, sourcePath))
				{
					continue;
				}

				recycleBinService.Recycle(oldFullPath, root.Path, mediaManagement.RecycleBinPath, timeProvider);
			}

			destinationPath = FinalizePlacement(placedPath, destinationPath);

			fileLinker.ApplyPermissions(destinationPath, mediaManagement.ChmodFile, isDirectory: false);
			fileLinker.ApplyPermissions(versionFolder, mediaManagement.ChmodFolder, isDirectory: true);

			if (mediaManagement.ImportExtraFiles)
			{
				ImportExtraFiles(sourcePath, destinationPath, mediaManagement.ExtraFileExtensions, mediaManagement.UseHardlinks);
			}
		}

		var isUpgrade = existing is not null;
		var newFile = new MovieFile
		{
			MovieId = movie.Id,
			MediaVersionId = version.Id,
			RelativePath = Path.GetRelativePath(versionFolder, destinationPath),
			Size = SafeLength(destinationPath),
			DateAdded = timeProvider.GetUtcNow().UtcDateTime,
			Quality = candidateQuality,
			Languages = [.. candidateLanguages],
			ReleaseGroup = candidateReleaseGroup,
			SceneName = parsed.FullTitle.Length > 0 ? parsed.FullTitle : downloadTitle,
			Edition = parsed.MovieReleaseData?.Edition,
			MediaInfo = mediaInfo
		};

		foreach (var oldFile in existingFiles)
		{
			db.MovieFiles.Remove(oldFile);
		}

		db.MovieFiles.Add(newFile);
		await db.SaveChangesAsync(cancellationToken);

		fileDateService.ApplyMovieFileDate(destinationPath, mediaManagement.FileDate, movie);
		await metadataWriter.WriteMovieAsync(movie, versionFolder, newFile, cancellationToken);

		foreach (var oldFile in existingFiles)
		{
			await eventBus.PublishAsync(new MovieFileDeletedEvent(movie.Id, version.Id, Path.Combine(versionFolder, oldFile.RelativePath), FileDeleteReason.UPGRADE), cancellationToken);
			await WriteHistoryAsync(HistoryEventType.DELETED, null, null, movie.Id, version.Id, oldFile.SceneName ?? oldFile.RelativePath, oldFile.Quality, oldFile.Languages, downloadId, null, cancellationToken);
		}

		await eventBus.PublishAsync(
			new MovieFileImportedEvent(movie.Id, version.Id, newFile.Id, destinationPath, parsed.FullTitle, downloadId, isUpgrade, candidateQuality, candidateLanguages, candidateReleaseGroup),
			cancellationToken);
		await WriteHistoryAsync(HistoryEventType.IMPORTED, null, null, movie.Id, version.Id, downloadTitle ?? parsed.FullTitle, candidateQuality, candidateLanguages, downloadId, JsonSerializer.Serialize(new { isUpgrade }), cancellationToken);

		return new ImportFileOutcome(sourcePath, true, destinationPath, isUpgrade, null, "Imported");
	}

	private ImportQualityDecision EvaluateQuality(
		QualityProfile qualityProfile,
		LanguageProfile languageProfile,
		IReadOnlyList<CustomFormat> customFormats,
		MediaManagementConfig mediaManagement,
		BaseRelease parsed,
		long size,
		QualityModel candidateQuality,
		IReadOnlyList<Language> candidateLanguages,
		QualityModel? existingQuality,
		IReadOnlyList<Language>? existingLanguages,
		string? existingSceneName,
		string? existingRelativePath,
		string? existingReleaseGroup,
		long? existingSize)
	{
		var candidateTitle = parsed.FullTitle.Length > 0 ? parsed.FullTitle : parsed.Title;
		var candidateScore = ComputeScore(qualityProfile, customFormats, new ReleaseContext(candidateTitle, parsed, size, parsed.Year));

		var existingScore = 0;
		if (existingQuality is not null && existingRelativePath is not null)
		{
			var existingRelease = ReconstructRelease(existingSceneName, existingRelativePath, existingQuality, existingLanguages ?? [], existingReleaseGroup);
			existingScore = ComputeScore(qualityProfile, customFormats, new ReleaseContext(existingSceneName ?? existingRelativePath, existingRelease, existingSize));
		}

		return ImportQualityEvaluator.Evaluate(
			qualityProfile,
			languageProfile,
			candidateQuality,
			candidateLanguages,
			candidateScore,
			existingQuality,
			existingLanguages,
			existingScore,
			mediaManagement.DownloadPropersAndRepacks);
	}

	private static int ComputeScore(QualityProfile profile, IReadOnlyList<CustomFormat> customFormats, ReleaseContext context)
		=> CustomFormatCalculator.Score(profile, CustomFormatCalculator.Match(customFormats, context));

	private BaseRelease ReconstructRelease(string? sceneName, string relativePath, QualityModel quality, IReadOnlyList<Language> languages, string? releaseGroup)
	{
		var title = sceneName ?? Path.GetFileNameWithoutExtension(relativePath);
		var parsed = SafeParse(title) ?? new BaseRelease { FullTitle = title, Title = title };
		return parsed with { Quality = quality, Languages = languages, ReleaseGroup = releaseGroup ?? parsed.ReleaseGroup };
	}

	private BaseRelease ParseWithFallback(string sourcePath, string? downloadTitle)
	{
		var fileNameNoExt = Path.GetFileNameWithoutExtension(sourcePath);
		var folderName = Path.GetFileName(Path.GetDirectoryName(sourcePath));
		BaseRelease? fallback = null;

		foreach (var candidate in new[] { fileNameNoExt, folderName, downloadTitle })
		{
			if (string.IsNullOrWhiteSpace(candidate))
			{
				continue;
			}

			var parsed = SafeParse(candidate);
			if (parsed is null)
			{
				continue;
			}

			fallback ??= parsed;
			if (HasUsefulMatchData(parsed))
			{
				return parsed;
			}
		}

		return fallback ?? new BaseRelease { FullTitle = fileNameNoExt, Title = fileNameNoExt };
	}

	private BaseRelease? SafeParse(string input)
	{
		try
		{
			return releaseParser.Parse(input);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogDebug(exception, "Could not parse release from {Input}", input);
			return null;
		}
	}

	private static bool HasUsefulMatchData(BaseRelease release)
		=> release.SeriesReleaseData is { } data && (data.Episodes.Count > 0 || data.AbsoluteEpisodes.Count > 0)
		   || release.MovieReleaseData is not null;

	private async Task<IReadOnlyList<Episode>> ResolveEpisodesAsync(
		Series series,
		IReadOnlyList<int> knownEpisodeIds,
		BaseRelease parsed,
		string fileNameNoExt,
		CancellationToken cancellationToken)
	{
		var episodes = await db.Episodes.Where(x => x.SeriesId == series.Id).ToListAsync(cancellationToken);
		var matched = series.Type == SeriesType.DAILY
			? EpisodeMatcher.MatchDaily(episodes, fileNameNoExt)
			: EpisodeMatcher.Match(episodes, series, parsed.SeriesReleaseData);

		if (matched.Count == 0 && knownEpisodeIds.Count == 1)
		{
			// The file's own name carries no episode numbers: fall back to the single known episode.
			var single = episodes.FirstOrDefault(x => x.Id == knownEpisodeIds[0]);
			return single is null ? [] : [single];
		}

		return knownEpisodeIds.Count > 0 ? [.. matched.Where(x => knownEpisodeIds.Contains(x.Id))] : matched;
	}

	private void ImportExtraFiles(string sourceVideoPath, string destinationVideoPath, string extraFileExtensions, bool useHardlinks)
	{
		var sourceDirectory = Path.GetDirectoryName(sourceVideoPath);
		var destinationDirectory = Path.GetDirectoryName(destinationVideoPath);
		if (sourceDirectory is null || destinationDirectory is null)
		{
			return;
		}

		var extensions = extraFileExtensions
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(x => x.StartsWith('.') ? x : "." + x)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var mainStem = Path.GetFileNameWithoutExtension(sourceVideoPath);
		var destinationStem = Path.GetFileNameWithoutExtension(destinationVideoPath);

		foreach (var extraFile in Directory.EnumerateFiles(sourceDirectory))
		{
			var extension = Path.GetExtension(extraFile);
			if (!extensions.Contains(extension))
			{
				continue;
			}

			var extraStem = Path.GetFileNameWithoutExtension(extraFile);
			if (!extraStem.StartsWith(mainStem, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var suffix = extraStem[mainStem.Length..];
			var language = ExtraFileLanguageDetector.Detect(suffix.TrimStart('.', '_', '-'));
			var languageSuffix = language is not null ? $".{language.Value.ToString().ToLowerInvariant()}" : suffix;
			var destinationExtraPath = Path.Combine(destinationDirectory, destinationStem + languageSuffix + extension);

			try
			{
				fileLinker.LinkOrMove(extraFile, destinationExtraPath, useHardlinks);
			}
			catch (IOException exception)
			{
				logger.LogWarning(exception, "Failed to import extra file {Path}", extraFile);
			}
		}
	}

	private async Task UpdateDownloadStateAsync(TrackedDownload download, IReadOnlyList<ImportFileOutcome> outcomes, CancellationToken cancellationToken)
	{
		if (outcomes.Count == 0)
		{
			await FailImportAsync(download, "No importable media files found in the download output", cancellationToken);
			await eventBus.PublishAsync(
				new ManualInteractionRequiredEvent(download.DownloadId, download.Title, download.SeriesId, download.MovieId, "No importable media files found"),
				cancellationToken);
			return;
		}

		if (outcomes.Any(x => x.Imported))
		{
			var allCovered = download.EpisodeIds.Count == 0
				|| (await db.Episodes.Where(x => download.EpisodeIds.Contains(x.Id))
					.Select(x => x.Files.Any(f => f.MediaVersionId == download.MediaVersionId))
					.ToListAsync(cancellationToken))
				.All(covered => covered);

			if (allCovered)
			{
				download.State = TrackedDownloadState.IMPORTED;
				download.Imported = true;
				download.StatusMessages = [];
			}
			else
			{
				download.State = TrackedDownloadState.IMPORT_PENDING;
				download.StatusMessages = [.. outcomes.Where(x => !x.Imported && x.Message is not null).Select(x => x.Message!)];
			}
		}
		else
		{
			download.State = TrackedDownloadState.FAILED_PENDING;
			download.StatusMessages = [.. outcomes.Where(x => x.Message is not null).Select(x => x.Message!)];
			await eventBus.PublishAsync(
				new ManualInteractionRequiredEvent(download.DownloadId, download.Title, download.SeriesId, download.MovieId, "No files could be imported"),
				cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task WriteHistoryAsync(
		HistoryEventType type,
		int? seriesId,
		int? episodeId,
		int? movieId,
		int? mediaVersionId,
		string sourceTitle,
		QualityModel? quality,
		IReadOnlyList<Language>? languages,
		string? downloadId,
		string? data,
		CancellationToken cancellationToken)
	{
		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = type,
			SeriesId = seriesId,
			EpisodeId = episodeId,
			MovieId = movieId,
			MediaVersionId = mediaVersionId,
			SourceTitle = sourceTitle,
			Quality = quality,
			Languages = languages?.ToList(),
			DownloadId = downloadId,
			Data = data,
			Date = timeProvider.GetUtcNow().UtcDateTime
		});
		await db.SaveChangesAsync(cancellationToken);
	}

	private static List<string> EnumerateCandidateFiles(string? outputPath)
	{
		if (string.IsNullOrWhiteSpace(outputPath))
		{
			return [];
		}

		List<string> allFiles;
		if (File.Exists(outputPath))
		{
			allFiles = [outputPath];
		}
		else if (Directory.Exists(outputPath))
		{
			allFiles = [.. Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories)];
		}
		else
		{
			return [];
		}

		var mediaFiles = allFiles
			.Where(x => !SampleFileDetector.IsIncompleteDownloadArtifact(x))
			.Where(x => MediaFileConstants.MediaFileExtensions.Contains(Path.GetExtension(x).ToLowerInvariant()))
			.ToList();

		return [.. mediaFiles.Where(x => !SampleFileDetector.IsSample(Path.GetFileName(x), SafeLength(x), mediaFiles.Count > 1))];
	}

	private static long SafeLength(string path)
	{
		try
		{
			return new FileInfo(path).Length;
		}
		catch (IOException)
		{
			return 0;
		}
	}

	private static void DeleteIfEmpty(string folder)
	{
		try
		{
			if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories).Any())
			{
				Directory.Delete(folder, recursive: true);
			}
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// best effort cleanup
		}
	}

	/// <summary>
	///     Places the source file at <paramref name="destinationPath" />, or under a temporary name when
	///     that name is already taken. Never deletes whatever currently occupies the destination: the
	///     caller recycles the old tracked file (if any) and then calls <see cref="FinalizePlacement" />.
	/// </summary>
	private string PlaceNewFile(string sourcePath, string destinationPath, ImportMode mode, bool useHardlinks)
	{
		if (PathsEqual(sourcePath, destinationPath))
		{
			return destinationPath;
		}

		var directory = Path.GetDirectoryName(destinationPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var placementPath = File.Exists(destinationPath) ? destinationPath + ".importing" : destinationPath;

		if (mode == ImportMode.COPY)
		{
			File.Copy(sourcePath, placementPath, overwrite: false);
		}
		else
		{
			fileLinker.LinkOrMove(sourcePath, placementPath, useHardlinks);
		}

		return placementPath;
	}

	/// <summary>
	///     Moves a file placed by <see cref="PlaceNewFile" /> to its final destination name. Never
	///     overwrites an existing file that is not the one just placed: if the destination is still taken
	///     (an untracked file, or one the caller chose not to recycle), an alternate name is used instead.
	/// </summary>
	private static string FinalizePlacement(string placedPath, string destinationPath)
	{
		if (PathsEqual(placedPath, destinationPath))
		{
			return placedPath;
		}

		var target = File.Exists(destinationPath) ? MakeAvailablePath(destinationPath) : destinationPath;
		File.Move(placedPath, target);
		return target;
	}

	private static string MakeAvailablePath(string path)
	{
		var directory = Path.GetDirectoryName(path) ?? string.Empty;
		var stem = Path.GetFileNameWithoutExtension(path);
		var extension = Path.GetExtension(path);
		for (var i = 1;; i++)
		{
			var candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
			if (!File.Exists(candidate))
			{
				return candidate;
			}
		}
	}

	private static bool PathsEqual(string a, string b)
		=> string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
