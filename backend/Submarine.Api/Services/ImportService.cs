using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Events;
using Submarine.Api.Models.Database;
using Submarine.Core.History;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Parser;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;

namespace Submarine.Api.Services;

/// <summary>
///     Imports the files of a completed <see cref="Core.Download.TrackedDownload" /> into the library
/// </summary>
public class ImportService
{
	private const long SampleSizeThreshold = 50L * 1024 * 1024;

	private readonly SubmarineDatabaseContext _context;
	private readonly SettingsService _settingsService;
	private readonly IParser<BaseRelease> _releaseParser;
	private readonly MediaNamingService _naming;
	private readonly HistoryService _historyService;
	private readonly IEventPublisher _eventPublisher;
	private readonly IMappingsClient _mappingsClient;
	private readonly ILogger<ImportService> _logger;

	public ImportService(SubmarineDatabaseContext context, SettingsService settingsService,
		IParser<BaseRelease> releaseParser, MediaNamingService naming, HistoryService historyService,
		IEventPublisher eventPublisher, IMappingsClient mappingsClient, ILogger<ImportService> logger)
	{
		_context = context;
		_settingsService = settingsService;
		_releaseParser = releaseParser;
		_naming = naming;
		_historyService = historyService;
		_eventPublisher = eventPublisher;
		_mappingsClient = mappingsClient;
		_logger = logger;
	}

	public async Task ImportTrackedDownloadAsync(int trackedDownloadId, CancellationToken cancellationToken = default)
	{
		var tracked = await _context.TrackedDownloads
			.FirstOrDefaultAsync(t => t.Id == trackedDownloadId, cancellationToken);

		if (tracked == null || tracked.Imported)
			return;

		if (string.IsNullOrEmpty(tracked.OutputPath))
		{
			_logger.LogWarning("Tracked download {Id} has no output path to import from", trackedDownloadId);
			return;
		}

		var files = CollectVideoFiles(tracked.OutputPath);

		if (files.Count == 0)
		{
			_logger.LogWarning("No importable video files found in {Path}", tracked.OutputPath);
			return;
		}

		var namingConfig = await _settingsService.GetNamingConfigAsync();
		var managementConfig = await _settingsService.GetMediaManagementConfigAsync();

		if (tracked.SeriesId is { } seriesId)
		{
			var series = await _context.Series.FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);
			var episodes = await _context.Episodes.Where(e => e.SeriesId == seriesId).ToListAsync(cancellationToken);

			if (series != null)
				foreach (var file in files)
					await ImportEpisodeFileAsync(tracked, series, episodes, file, namingConfig, managementConfig,
						files.Count == 1, cancellationToken);
		}
		else if (tracked.MovieId is { } movieId)
		{
			var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == movieId, cancellationToken);

			if (movie != null)
				foreach (var file in files)
					await ImportMovieFileAsync(tracked, movie, file, namingConfig, managementConfig, cancellationToken);
		}

		tracked.Imported = true;
		await _context.SaveChangesAsync(cancellationToken);

		var importedPath = tracked.SeriesId != null
			? await _context.Series.AsNoTracking().Where(s => s.Id == tracked.SeriesId).Select(s => s.Path)
				.FirstOrDefaultAsync(cancellationToken)
			: await _context.Movies.AsNoTracking().Where(m => m.Id == tracked.MovieId).Select(m => m.Path)
				.FirstOrDefaultAsync(cancellationToken);

		if (importedPath != null)
			await _eventPublisher.PublishAsync(
				new MediaImportedEvent(tracked.SeriesId, tracked.MovieId, importedPath, tracked.Title),
				cancellationToken);
	}

	private async Task ImportEpisodeFileAsync(Core.Download.TrackedDownload tracked, Series series,
		List<Episode> seriesEpisodes, string sourceFile, Core.Config.NamingConfig namingConfig,
		Core.Config.MediaManagementConfig managementConfig, bool singleFile, CancellationToken cancellationToken)
	{
		var parsed = TryParse(sourceFile);
		var episodes = await ResolveEpisodesAsync(series, seriesEpisodes, parsed, tracked, singleFile,
			cancellationToken);

		if (episodes.Count == 0)
		{
			_logger.LogWarning("Could not match {File} to any episode of series {SeriesId}", sourceFile, series.Id);
			return;
		}

		var ordered = episodes.OrderBy(e => e.EpisodeNumber).ToList();
		var quality = tracked.Quality;
		var languages = tracked.Languages.Count > 0 ? tracked.Languages : parsed?.Languages.ToList() ?? new();
		var releaseGroup = tracked.ReleaseGroup ?? parsed?.ReleaseGroup;

		var extension = Path.GetExtension(sourceFile);
		string fileName;
		bool namedFromPlaceholder;

		if (namingConfig.RenameEpisodes)
		{
			var rendered = _naming.RenderEpisodeFile(series, ordered, quality, languages, releaseGroup, namingConfig);
			fileName = rendered.Name;
			namedFromPlaceholder = rendered.UsedPlaceholderTitle;
		}
		else
		{
			fileName = Path.GetFileNameWithoutExtension(sourceFile);
			namedFromPlaceholder = false;
		}

		var seasonFolder = series.SeasonFolder
			? _naming.RenderSeasonFolder(series, ordered[0].SeasonNumber, namingConfig)
			: "";

		var destination = Path.Combine(series.Path, seasonFolder, fileName + extension);

		await ReplaceExistingEpisodeFilesAsync(series, ordered, cancellationToken);

		FileLinker.Place(sourceFile, destination, managementConfig.UseHardlinks);

		var episodeFile = new EpisodeFile
		{
			SeriesId = series.Id,
			RelativePath = Path.GetRelativePath(series.Path, destination),
			Size = new FileInfo(destination).Length,
			DateAdded = DateTimeOffset.UtcNow,
			Quality = quality,
			Languages = languages.ToList(),
			ReleaseGroup = releaseGroup,
			NamedFromPlaceholder = namedFromPlaceholder
		};

		_context.EpisodeFiles.Add(episodeFile);
		await _context.SaveChangesAsync(cancellationToken);

		foreach (var episode in ordered)
			episode.EpisodeFileId = episodeFile.Id;

		await _context.SaveChangesAsync(cancellationToken);

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.IMPORTED,
			SeriesId = series.Id,
			EpisodeId = ordered.Count == 1 ? ordered[0].Id : null,
			SourceTitle = tracked.ReleaseTitle,
			Quality = quality,
			Languages = languages.ToList(),
			Data = new Dictionary<string, string>
			{
				["path"] = episodeFile.RelativePath, ["downloadId"] = tracked.DownloadId
			}
		}, cancellationToken);
	}

	private async Task ImportMovieFileAsync(Core.Download.TrackedDownload tracked, Movie movie, string sourceFile,
		Core.Config.NamingConfig namingConfig, Core.Config.MediaManagementConfig managementConfig,
		CancellationToken cancellationToken)
	{
		var parsed = TryParse(sourceFile);
		var quality = tracked.Quality;
		var languages = tracked.Languages.Count > 0 ? tracked.Languages : parsed?.Languages.ToList() ?? new();
		var releaseGroup = tracked.ReleaseGroup ?? parsed?.ReleaseGroup;
		var edition = parsed?.MovieReleaseData?.Edition;

		var extension = Path.GetExtension(sourceFile);

		var fileName = namingConfig.RenameEpisodes
			? _naming.RenderMovieFile(movie, quality, languages, releaseGroup, edition, namingConfig).Name
			: Path.GetFileNameWithoutExtension(sourceFile);

		var destination = Path.Combine(movie.Path, fileName + extension);

		if (movie.MovieFileId is { } oldId)
			await ReplaceExistingMovieFileAsync(movie, oldId, cancellationToken);

		FileLinker.Place(sourceFile, destination, managementConfig.UseHardlinks);

		var movieFile = new MovieFile
		{
			MovieId = movie.Id,
			RelativePath = Path.GetRelativePath(movie.Path, destination),
			Size = new FileInfo(destination).Length,
			DateAdded = DateTimeOffset.UtcNow,
			Quality = quality,
			Languages = languages.ToList(),
			ReleaseGroup = releaseGroup,
			Edition = edition
		};

		_context.MovieFiles.Add(movieFile);
		await _context.SaveChangesAsync(cancellationToken);

		movie.MovieFileId = movieFile.Id;
		await _context.SaveChangesAsync(cancellationToken);

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.IMPORTED,
			MovieId = movie.Id,
			SourceTitle = tracked.ReleaseTitle,
			Quality = quality,
			Languages = languages.ToList(),
			Data = new Dictionary<string, string>
			{
				["path"] = movieFile.RelativePath, ["downloadId"] = tracked.DownloadId
			}
		}, cancellationToken);
	}

	private async Task ReplaceExistingEpisodeFilesAsync(Series series, IReadOnlyList<Episode> episodes,
		CancellationToken cancellationToken)
	{
		var fileIds = episodes.Where(e => e.EpisodeFileId != null)
			.Select(e => e.EpisodeFileId!.Value)
			.Distinct()
			.ToList();

		if (fileIds.Count == 0)
			return;

		foreach (var episode in episodes)
			episode.EpisodeFileId = null;

		var oldFiles = await _context.EpisodeFiles.Where(f => fileIds.Contains(f.Id)).ToListAsync(cancellationToken);

		foreach (var oldFile in oldFiles)
		{
			DeleteFromDisk(Path.Combine(series.Path, oldFile.RelativePath));
			_context.EpisodeFiles.Remove(oldFile);
		}

		await _context.SaveChangesAsync(cancellationToken);
	}

	private async Task ReplaceExistingMovieFileAsync(Movie movie, int oldFileId, CancellationToken cancellationToken)
	{
		movie.MovieFileId = null;

		var oldFile = await _context.MovieFiles.FirstOrDefaultAsync(f => f.Id == oldFileId, cancellationToken);

		if (oldFile != null)
		{
			DeleteFromDisk(Path.Combine(movie.Path, oldFile.RelativePath));
			_context.MovieFiles.Remove(oldFile);
		}

		await _context.SaveChangesAsync(cancellationToken);
	}

	private static void DeleteFromDisk(string path)
	{
		if (File.Exists(path))
			File.Delete(path);
	}

	private async Task<List<Episode>> ResolveEpisodesAsync(Series series, List<Episode> seriesEpisodes,
		BaseRelease? parsed, Core.Download.TrackedDownload tracked, bool singleFile, CancellationToken cancellationToken)
	{
		if (singleFile && tracked.EpisodeIds.Count > 0)
			return seriesEpisodes.Where(e => tracked.EpisodeIds.Contains(e.Id)).ToList();

		var matched = new List<Episode>();

		if (parsed?.SeriesReleaseData is { } data)
		{
			if (data.Seasons.Count > 0 && data.Episodes.Count > 0)
			{
				matched = seriesEpisodes
					.Where(e => data.Seasons.Contains(e.SeasonNumber) && data.Episodes.Contains(e.EpisodeNumber))
					.ToList();

				if (matched.Count == 0 && series.Type != SeriesType.ANIME)
					matched = await ResolveSceneEpisodesAsync(series, seriesEpisodes, data, cancellationToken);
			}

			if (matched.Count == 0 && series.Type == SeriesType.ANIME && data.AbsoluteEpisodes.Count > 0)
			{
				matched = await ResolveAniListEpisodesAsync(series, seriesEpisodes, data, cancellationToken);

				if (matched.Count == 0)
					matched = MatchAbsolute(seriesEpisodes, data);
			}
			else if (matched.Count == 0 && data.AbsoluteEpisodes.Count > 0)
			{
				matched = MatchAbsolute(seriesEpisodes, data);
			}
		}

		if (matched.Count == 0 && tracked.EpisodeIds.Count > 0)
			matched = seriesEpisodes.Where(e => tracked.EpisodeIds.Contains(e.Id)).ToList();

		return matched;
	}

	private static List<Episode> MatchAbsolute(List<Episode> seriesEpisodes, Core.Release.SeriesReleaseData data)
		=> seriesEpisodes
			.Where(e => e.AbsoluteEpisodeNumber != null &&
			            data.AbsoluteEpisodes.Contains(e.AbsoluteEpisodeNumber.Value))
			.ToList();

	private async Task<List<Episode>> ResolveAniListEpisodesAsync(Series series, List<Episode> seriesEpisodes,
		Core.Release.SeriesReleaseData data, CancellationToken cancellationToken)
	{
		var mappings = await TryGetAniListMappingsAsync(series.TvdbId, cancellationToken);

		if (mappings.Count == 0)
			return new List<Episode>();

		var matched = new List<Episode>();

		foreach (var absolute in data.AbsoluteEpisodes)
		foreach (var mapping in mappings)
		{
			// The parsed number may be the TVDB absolute number or the AniList entry episode number
			foreach (var aniListEpisode in new[] { absolute - mapping.AbsoluteOffset, absolute })
			{
				if (aniListEpisode < 1 || (mapping.EpisodeCount != null && aniListEpisode > mapping.EpisodeCount))
					continue;

				var episode = seriesEpisodes.FirstOrDefault(e => e.SeasonNumber == mapping.TvdbSeason &&
				                                                  e.EpisodeNumber == mapping.EpisodeStart +
				                                                  aniListEpisode - 1);

				if (episode == null || matched.Contains(episode))
					continue;

				matched.Add(episode);
				break;
			}
		}

		return matched;
	}

	private async Task<List<Episode>> ResolveSceneEpisodesAsync(Series series, List<Episode> seriesEpisodes,
		Core.Release.SeriesReleaseData data, CancellationToken cancellationToken)
	{
		var set = await TryGetSceneMappingsAsync(series.TvdbId, cancellationToken);

		if (set == null)
			return new List<Episode>();

		var matched = new List<Episode>();

		foreach (var sceneSeason in data.Seasons)
		foreach (var sceneEpisode in data.Episodes)
		{
			var (season, episode) = ResolveTvdbFromScene(set, sceneSeason, sceneEpisode);

			var target = seriesEpisodes.FirstOrDefault(e => e.SeasonNumber == season && e.EpisodeNumber == episode);

			if (target != null && !matched.Contains(target))
				matched.Add(target);
		}

		return matched;
	}

	private static (int Season, int Episode) ResolveTvdbFromScene(SceneMappingSet set, int sceneSeason, int sceneEpisode)
	{
		var episodeOverride = set.EpisodeMappings
			.FirstOrDefault(m => m.SceneSeasonNumber == sceneSeason && m.SceneEpisodeNumber == sceneEpisode);

		if (episodeOverride != null)
			return (episodeOverride.SeasonNumber, episodeOverride.EpisodeNumber);

		var mapping = set.Mappings
			.Where(m => m.SceneSeasonNumber == sceneSeason || m.SceneSeasonNumber == null)
			.OrderByDescending(m => m.SceneSeasonNumber != null)
			.FirstOrDefault();

		if (mapping == null)
			return (sceneSeason, sceneEpisode);

		return (mapping.SeasonNumber ?? sceneSeason, sceneEpisode - mapping.EpisodeOffset);
	}

	private async Task<SceneMappingSet?> TryGetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken)
	{
		try
		{
			return await _mappingsClient.GetSceneMappingsAsync(tvdbId, cancellationToken);
		}
		catch (Exception ex)
		{
			if (cancellationToken.IsCancellationRequested)
				throw;

			_logger.LogDebug(ex, "Fetching scene mappings for series {TvdbId} failed", tvdbId);
			return null;
		}
	}

	private async Task<IReadOnlyList<Submarine.Mappings.Contracts.AniListMappingResource>> TryGetAniListMappingsAsync(
		int tvdbId, CancellationToken cancellationToken)
	{
		try
		{
			return await _mappingsClient.GetAniListMappingsAsync(tvdbId, cancellationToken);
		}
		catch (Exception ex)
		{
			if (cancellationToken.IsCancellationRequested)
				throw;

			_logger.LogDebug(ex, "Fetching AniList mappings for series {TvdbId} failed", tvdbId);
			return Array.Empty<Submarine.Mappings.Contracts.AniListMappingResource>();
		}
	}

	private BaseRelease? TryParse(string sourceFile)
	{
		try
		{
			return _releaseParser.Parse(Path.GetFileNameWithoutExtension(sourceFile));
		}
		catch (NotParsableReleaseException)
		{
			return null;
		}
	}

	private static List<string> CollectVideoFiles(string outputPath)
	{
		IEnumerable<string> files;

		if (File.Exists(outputPath))
			files = new[] { outputPath };
		else if (Directory.Exists(outputPath))
			files = Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories);
		else
			return new List<string>();

		var media = files
			.Where(f => MediaFileConstants.MediaFileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
			.Where(f => !Path.GetFileName(f).Contains("sample", StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (media.Count > 1)
			media = media.Where(f => new FileInfo(f).Length >= SampleSizeThreshold).ToList();

		return media;
	}
}
