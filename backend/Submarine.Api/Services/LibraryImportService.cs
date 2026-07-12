using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Core.History;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Parser;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;

namespace Submarine.Api.Services;

/// <summary>
///     Adopts an already organized library on disk: proposes matches for each media folder and registers the
///     existing files in place without moving or renaming them.
/// </summary>
public class LibraryImportService
{
	private readonly SubmarineDatabaseContext _context;
	private readonly SeriesService _seriesService;
	private readonly MovieService _movieService;
	private readonly IMetadataClient _metadataClient;
	private readonly IParser<BaseRelease> _releaseParser;
	private readonly HistoryService _historyService;
	private readonly ILogger<LibraryImportService> _logger;

	public LibraryImportService(SubmarineDatabaseContext context, SeriesService seriesService,
		MovieService movieService, IMetadataClient metadataClient, IParser<BaseRelease> releaseParser,
		HistoryService historyService, ILogger<LibraryImportService> logger)
	{
		_context = context;
		_seriesService = seriesService;
		_movieService = movieService;
		_metadataClient = metadataClient;
		_releaseParser = releaseParser;
		_historyService = historyService;
		_logger = logger;
	}

	public async Task<IReadOnlyList<LibraryImportProposal>> ScanAsync(LibraryImportScanRequest request,
		CancellationToken cancellationToken = default)
	{
		var basePath = await ResolveScanPathAsync(request.Path, request.RootFolderId, cancellationToken);

		if (!Directory.Exists(basePath))
			throw new BadRequestException($"path '{basePath}' does not exist");

		var proposals = new List<LibraryImportProposal>();

		foreach (var folder in Directory.EnumerateDirectories(basePath).OrderBy(f => f, StringComparer.Ordinal))
			proposals.Add(await BuildProposalAsync(folder, request.MediaKind, cancellationToken));

		return proposals;
	}

	/// <summary>
	///     Adopts the requested folders sequentially. Folders can hold many files, so callers should batch large
	///     libraries across multiple requests.
	/// </summary>
	public async Task<LibraryImportResponse> ImportAsync(LibraryImportRequest request,
		CancellationToken cancellationToken = default)
	{
		var results = new List<LibraryImportItemResult>();

		foreach (var item in request.Items)
			try
			{
				var mediaId = await ImportItemAsync(item, request.MediaKind, cancellationToken);
				results.Add(new LibraryImportItemResult(item.Folder, true, mediaId, null));
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Library import of folder {Folder} failed", item.Folder);
				results.Add(new LibraryImportItemResult(item.Folder, false, null, ex.Message));
			}

		return new LibraryImportResponse(results);
	}

	private async Task<int> ImportItemAsync(LibraryImportItem item, MediaKind mediaKind,
		CancellationToken cancellationToken)
	{
		if (mediaKind == MediaKind.SERIES)
			return await ImportSeriesFolderAsync(item, cancellationToken);

		return await ImportMovieFolderAsync(item, cancellationToken);
	}

	private async Task<int> ImportSeriesFolderAsync(LibraryImportItem item, CancellationToken cancellationToken)
	{
		if (item.TvdbId is not { } tvdbId)
			throw new BadRequestException("TvdbId is required to import a series folder");

		var series = await _seriesService.AddAsync(new AddSeriesRequest
		{
			TvdbId = tvdbId,
			Path = item.Folder,
			QualityProfileId = item.QualityProfileId,
			LanguageProfileId = item.LanguageProfileId,
			Monitored = item.MonitorExisting,
			Tags = item.Tags
		});

		var version = series.Versions.OrderBy(v => v.Id).First();
		var episodes = series.Episodes.ToList();

		foreach (var file in CollectVideoFiles(item.Folder))
			await RegisterEpisodeFileInPlaceAsync(series, version, episodes, file, cancellationToken);

		return series.Id;
	}

	private async Task<int> ImportMovieFolderAsync(LibraryImportItem item, CancellationToken cancellationToken)
	{
		if (item.TmdbId is not { } tmdbId)
			throw new BadRequestException("TmdbId is required to import a movie folder");

		var movie = await _movieService.AddAsync(new AddMovieRequest
		{
			TmdbId = tmdbId,
			Path = item.Folder,
			QualityProfileId = item.QualityProfileId,
			LanguageProfileId = item.LanguageProfileId,
			Monitored = item.MonitorExisting,
			Tags = item.Tags
		});

		var version = movie.Versions.OrderBy(v => v.Id).First();
		var files = CollectVideoFiles(item.Folder);

		if (files.Count > 0)
			await RegisterMovieFileInPlaceAsync(movie, version, LargestFile(files), cancellationToken);

		return movie.Id;
	}

	private async Task RegisterEpisodeFileInPlaceAsync(Series series, MediaVersion version, List<Episode> episodes,
		string file, CancellationToken cancellationToken)
	{
		var parsed = TryParse(file);
		var matched = MatchEpisodes(episodes, parsed);

		if (matched.Count == 0)
		{
			_logger.LogWarning("Could not match {File} to any episode of series {SeriesId}", file, series.Id);
			return;
		}

		var ordered = matched.OrderBy(e => e.EpisodeNumber).ToList();
		var quality = parsed?.Quality ?? UnknownQuality();
		var languages = parsed?.Languages.ToList() ?? new();

		if (!TryGetRelativePath(version.Path, file, out var relativePath))
		{
			_logger.LogWarning("Skipping {File}: outside version path {VersionPath}", file, version.Path);
			return;
		}

		_context.EpisodeFiles.Add(new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = relativePath,
			Size = new FileInfo(file).Length,
			DateAdded = DateTimeOffset.UtcNow,
			Quality = quality,
			Languages = languages,
			ReleaseGroup = parsed?.ReleaseGroup,
			NamedFromPlaceholder = false,
			Episodes = ordered
		});

		await _context.SaveChangesAsync(cancellationToken);

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.IMPORTED,
			SeriesId = series.Id,
			EpisodeId = ordered.Count == 1 ? ordered[0].Id : null,
			SourceTitle = Path.GetFileName(file),
			Quality = quality,
			Languages = languages,
			Data = new Dictionary<string, string>
			{
				["libraryImport"] = "true", ["path"] = relativePath, ["version"] = version.Name
			}
		}, cancellationToken);
	}

	private async Task RegisterMovieFileInPlaceAsync(Movie movie, MediaVersion version, string file,
		CancellationToken cancellationToken)
	{
		var parsed = TryParse(file);
		var quality = parsed?.Quality ?? UnknownQuality();
		var languages = parsed?.Languages.ToList() ?? new();

		if (!TryGetRelativePath(version.Path, file, out var relativePath))
		{
			_logger.LogWarning("Skipping {File}: outside version path {VersionPath}", file, version.Path);
			return;
		}

		_context.MovieFiles.Add(new MovieFile
		{
			MovieId = movie.Id,
			MediaVersionId = version.Id,
			RelativePath = relativePath,
			Size = new FileInfo(file).Length,
			DateAdded = DateTimeOffset.UtcNow,
			Quality = quality,
			Languages = languages,
			ReleaseGroup = parsed?.ReleaseGroup,
			Edition = parsed?.MovieReleaseData?.Edition
		});

		await _context.SaveChangesAsync(cancellationToken);

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.IMPORTED,
			MovieId = movie.Id,
			SourceTitle = Path.GetFileName(file),
			Quality = quality,
			Languages = languages,
			Data = new Dictionary<string, string>
			{
				["libraryImport"] = "true", ["path"] = relativePath, ["version"] = version.Name
			}
		}, cancellationToken);
	}

	private async Task<LibraryImportProposal> BuildProposalAsync(string folder, MediaKind mediaKind,
		CancellationToken cancellationToken)
	{
		var folderName = new DirectoryInfo(folder).Name;
		var parsed = TryParse(folderName);
		var title = parsed?.Title ?? folderName;

		var files = CollectVideoFiles(folder).Select(BuildProposalFile).ToList();

		return mediaKind == MediaKind.SERIES
			? await BuildSeriesProposalAsync(folder, title, parsed?.Year, files, cancellationToken)
			: await BuildMovieProposalAsync(folder, title, parsed?.Year, files, cancellationToken);
	}

	private async Task<LibraryImportProposal> BuildSeriesProposalAsync(string folder, string title, int? year,
		IReadOnlyList<LibraryImportProposalFile> files, CancellationToken cancellationToken)
	{
		var results = await _metadataClient.SearchSeriesAsync(title, MetadataProvider.TVDB, cancellationToken);
		var best = results.FirstOrDefault();

		var alternatives = results
			.Skip(1)
			.Take(5)
			.Select(s => new LibraryImportAlternative(s.TvdbId, s.Title))
			.ToList();

		return new LibraryImportProposal(folder, best?.TvdbId, null, best?.Title, best?.Year ?? year, alternatives,
			files);
	}

	private async Task<LibraryImportProposal> BuildMovieProposalAsync(string folder, string title, int? year,
		IReadOnlyList<LibraryImportProposalFile> files, CancellationToken cancellationToken)
	{
		var results = await _metadataClient.SearchMoviesAsync(title, cancellationToken);
		var best = results.FirstOrDefault();

		var alternatives = results
			.Skip(1)
			.Take(5)
			.Select(m => new LibraryImportAlternative(m.TmdbId, m.Title))
			.ToList();

		return new LibraryImportProposal(folder, null, best?.TmdbId, best?.Title, best?.Year ?? year, alternatives,
			files);
	}

	private LibraryImportProposalFile BuildProposalFile(string file)
	{
		var parsed = TryParse(file);
		var data = parsed?.SeriesReleaseData;

		return new LibraryImportProposalFile(
			file,
			data is { Seasons.Count: > 0 } ? data.Seasons[0] : null,
			data?.Episodes ?? Array.Empty<int>(),
			data?.AbsoluteEpisodes ?? Array.Empty<int>(),
			parsed?.Quality.Resolution.Name ?? "Unknown");
	}

	private async Task<string> ResolveScanPathAsync(string? requestPath, int? rootFolderId,
		CancellationToken cancellationToken)
	{
		if (rootFolderId != null)
		{
			var rootFolder = await _context.RootFolders.AsNoTracking()
				.FirstOrDefaultAsync(r => r.Id == rootFolderId, cancellationToken);

			if (rootFolder == null)
				throw new BadRequestException($"Root folder with id '{rootFolderId}' does not exist");

			return rootFolder.Path;
		}

		if (!string.IsNullOrWhiteSpace(requestPath))
			return requestPath;

		throw new BadRequestException("either Path or RootFolderId must be provided");
	}

	private static List<Episode> MatchEpisodes(List<Episode> episodes, BaseRelease? parsed)
	{
		if (parsed?.SeriesReleaseData is not { } data)
			return new List<Episode>();

		var matched = episodes
			.Where(e => data.Seasons.Contains(e.SeasonNumber) && data.Episodes.Contains(e.EpisodeNumber))
			.ToList();

		if (matched.Count == 0 && data.AbsoluteEpisodes.Count > 0)
			matched = episodes
				.Where(e => e.AbsoluteEpisodeNumber != null &&
				            data.AbsoluteEpisodes.Contains(e.AbsoluteEpisodeNumber.Value))
				.ToList();

		return matched;
	}

	private BaseRelease? TryParse(string input)
	{
		try
		{
			return _releaseParser.Parse(Path.GetFileNameWithoutExtension(input));
		}
		catch (NotParsableReleaseException)
		{
			return null;
		}
	}

	// guards against a file resolving outside the version folder, which would otherwise store a traversing ".." path
	internal static bool TryGetRelativePath(string versionPath, string file, out string relativePath)
	{
		var root = Path.GetFullPath(versionPath);
		var full = Path.GetFullPath(file);
		var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

		var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

		if (!full.StartsWith(prefix, comparison))
		{
			relativePath = string.Empty;
			return false;
		}

		relativePath = Path.GetRelativePath(root, full);
		return true;
	}

	private static string LargestFile(IEnumerable<string> files)
		=> files.OrderByDescending(f => new FileInfo(f).Length).First();

	private static QualityModel UnknownQuality()
		=> new(new QualityResolutionModel(), new Revision());

	private static List<string> CollectVideoFiles(string folder)
		=> Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
			.Where(f => MediaFileConstants.MediaFileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
			.Where(f => !Path.GetFileName(f).Contains("sample", StringComparison.OrdinalIgnoreCase))
			.OrderBy(f => f, StringComparer.Ordinal)
			.ToList();
}
