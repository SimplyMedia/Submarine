using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Parser;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;

namespace Submarine.Api.Services;

/// <summary>
///     Drives user-chosen imports: lists candidate files with parsed guesses and imports specific files against
///     the media and episodes the user picked, through the shared <see cref="ImportService" /> placement path.
/// </summary>
public class ManualImportService
{
	private readonly SubmarineDatabaseContext _context;
	private readonly ImportService _importService;
	private readonly SettingsService _settingsService;
	private readonly IParser<BaseRelease> _releaseParser;

	public ManualImportService(SubmarineDatabaseContext context, ImportService importService,
		SettingsService settingsService, IParser<BaseRelease> releaseParser)
	{
		_context = context;
		_importService = importService;
		_settingsService = settingsService;
		_releaseParser = releaseParser;
	}

	public async Task<IReadOnlyList<ManualImportCandidate>> ListAsync(string? folder, int? trackedDownloadId,
		CancellationToken cancellationToken = default)
	{
		if (trackedDownloadId is { } id)
			return await ListFromTrackedAsync(id, cancellationToken);

		if (!string.IsNullOrWhiteSpace(folder))
			return await ListFromFolderAsync(folder, cancellationToken);

		throw new BadRequestException("either folder or trackedDownloadId must be provided");
	}

	public async Task<ManualImportResponse> ImportAsync(ManualImportRequest request,
		CancellationToken cancellationToken = default)
	{
		var namingConfig = await _settingsService.GetNamingConfigAsync();
		var managementConfig = await _settingsService.GetMediaManagementConfigAsync();

		// Validate every file up front so a mismatch fails the request before any file is placed.
		var plans = new List<ManualImportPlan>();

		foreach (var file in request.Files)
			plans.Add(await BuildPlanAsync(file, cancellationToken));

		var results = new List<ManualImportFileResult>();

		foreach (var plan in plans)
			try
			{
				await ImportPlanAsync(plan, namingConfig, managementConfig, cancellationToken);
				results.Add(new ManualImportFileResult(plan.File.Path, true, null));
			}
			catch (Exception ex)
			{
				results.Add(new ManualImportFileResult(plan.File.Path, false, ex.Message));
			}

		if (request.TrackedDownloadId is { } trackedId)
			await MarkTrackedImportedAsync(trackedId, cancellationToken);

		return new ManualImportResponse(results);
	}

	private async Task ImportPlanAsync(ManualImportPlan plan, Core.Config.NamingConfig namingConfig,
		Core.Config.MediaManagementConfig managementConfig, CancellationToken cancellationToken)
	{
		var sourceTitle = Path.GetFileNameWithoutExtension(plan.File.Path);
		var data = new Dictionary<string, string> { ["manualImport"] = "true" };

		if (plan.Series != null)
			await _importService.ImportEpisodeFileAsync(plan.Series, plan.Version, plan.Episodes, plan.File.Path,
				plan.Quality, plan.Languages, plan.ReleaseGroup, sourceTitle, data, namingConfig, managementConfig,
				cancellationToken);
		else
			await _importService.ImportMovieFileAsync(plan.Movie!, plan.Version, plan.File.Path, plan.Quality,
				plan.Languages, plan.ReleaseGroup, plan.Edition, sourceTitle, data, namingConfig, managementConfig,
				cancellationToken);
	}

	private async Task<ManualImportPlan> BuildPlanAsync(ManualImportFile file, CancellationToken cancellationToken)
	{
		if (file.SeriesId != null == (file.MovieId != null))
			throw new BadRequestException("exactly one of SeriesId or MovieId must be provided");

		var version = await _context.Versions.AsNoTracking()
			.FirstOrDefaultAsync(v => v.Id == file.MediaVersionId, cancellationToken);

		if (version == null)
			throw new BadRequestException($"version with id '{file.MediaVersionId}' does not exist");

		var parsed = TryParse(file.Path);
		var quality = BuildQuality(file.Quality, parsed);
		var languages = parsed?.Languages.ToList() ?? new List<Language>();
		var releaseGroup = parsed?.ReleaseGroup;

		if (file.SeriesId is { } seriesId)
		{
			var series = await _context.Series.FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

			if (series == null)
				throw new BadRequestException($"series with id '{seriesId}' does not exist");

			if (version.SeriesId != seriesId)
				throw new BadRequestException($"version '{file.MediaVersionId}' does not belong to series '{seriesId}'");

			var episodes = await _context.Episodes
				.Where(e => e.SeriesId == seriesId && file.EpisodeIds.Contains(e.Id))
				.ToListAsync(cancellationToken);

			if (episodes.Count != file.EpisodeIds.Distinct().Count())
				throw new BadRequestException("one or more episodes do not exist for the series");

			if (episodes.Count == 0)
				throw new BadRequestException("at least one episode must be provided");

			return new ManualImportPlan(file, series, null, version, episodes, quality, languages, releaseGroup, null);
		}

		var movieId = file.MovieId!.Value;
		var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == movieId, cancellationToken);

		if (movie == null)
			throw new BadRequestException($"movie with id '{movieId}' does not exist");

		if (version.MovieId != movieId)
			throw new BadRequestException($"version '{file.MediaVersionId}' does not belong to movie '{movieId}'");

		return new ManualImportPlan(file, null, movie, version, new List<Episode>(), quality, languages, releaseGroup,
			parsed?.MovieReleaseData?.Edition);
	}

	private async Task<IReadOnlyList<ManualImportCandidate>> ListFromTrackedAsync(int trackedDownloadId,
		CancellationToken cancellationToken)
	{
		var tracked = await _context.TrackedDownloads.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == trackedDownloadId, cancellationToken);

		if (tracked == null)
			throw new NotFoundException();

		if (string.IsNullOrEmpty(tracked.OutputPath) || !Directory.Exists(tracked.OutputPath))
			return Array.Empty<ManualImportCandidate>();

		var episodes = tracked.SeriesId is { } seriesId
			? await _context.Episodes.AsNoTracking().Where(e => e.SeriesId == seriesId).ToListAsync(cancellationToken)
			: new List<Episode>();

		var candidates = new List<ManualImportCandidate>();

		foreach (var file in CollectVideoFiles(tracked.OutputPath))
		{
			var parsed = TryParse(file);
			var suggestedEpisodeIds = tracked.SeriesId != null
				? SuggestEpisodeIds(episodes, parsed, tracked.EpisodeIds)
				: Array.Empty<int>();

			candidates.Add(BuildCandidate(file, parsed, tracked.SeriesId, tracked.MovieId, suggestedEpisodeIds));
		}

		return candidates;
	}

	private async Task<IReadOnlyList<ManualImportCandidate>> ListFromFolderAsync(string folder,
		CancellationToken cancellationToken)
	{
		if (!Directory.Exists(folder) && !File.Exists(folder))
			throw new BadRequestException($"path '{folder}' does not exist");

		var series = await _context.Series.AsNoTracking().Select(s => new { s.Id, s.Title })
			.ToListAsync(cancellationToken);
		var movies = await _context.Movies.AsNoTracking().Select(m => new { m.Id, m.Title })
			.ToListAsync(cancellationToken);

		var candidates = new List<ManualImportCandidate>();

		foreach (var file in CollectVideoFiles(folder))
		{
			var parsed = TryParse(file);
			var normalized = Normalize(parsed?.Title ?? Path.GetFileNameWithoutExtension(file));

			var seriesMatch = series.FirstOrDefault(s => Normalize(s.Title) == normalized);
			var movieMatch = seriesMatch == null
				? movies.FirstOrDefault(m => Normalize(m.Title) == normalized)
				: null;

			int[] suggestedEpisodeIds = Array.Empty<int>();

			if (seriesMatch != null)
			{
				var episodes = await _context.Episodes.AsNoTracking()
					.Where(e => e.SeriesId == seriesMatch.Id)
					.ToListAsync(cancellationToken);

				suggestedEpisodeIds = SuggestEpisodeIds(episodes, parsed, new List<int>());
			}

			candidates.Add(BuildCandidate(file, parsed, seriesMatch?.Id, movieMatch?.Id, suggestedEpisodeIds));
		}

		return candidates;
	}

	private static ManualImportCandidate BuildCandidate(string file, BaseRelease? parsed, int? seriesId, int? movieId,
		IReadOnlyList<int> suggestedEpisodeIds)
	{
		var data = parsed?.SeriesReleaseData;

		return new ManualImportCandidate(
			file,
			new FileInfo(file).Length,
			parsed?.Title,
			data is { Seasons.Count: > 0 } ? data.Seasons[0] : null,
			data?.Episodes ?? Array.Empty<int>(),
			seriesId,
			movieId,
			suggestedEpisodeIds,
			parsed?.Quality.Resolution.Name ?? "Unknown",
			parsed?.Languages.ToList() ?? new List<Language>(),
			parsed?.ReleaseGroup);
	}

	private static int[] SuggestEpisodeIds(List<Episode> episodes, BaseRelease? parsed, List<int> fallback)
	{
		if (parsed?.SeriesReleaseData is { } data)
		{
			var matched = episodes
				.Where(e => data.Seasons.Contains(e.SeasonNumber) && data.Episodes.Contains(e.EpisodeNumber))
				.Select(e => e.Id)
				.ToArray();

			if (matched.Length > 0)
				return matched;
		}

		return fallback.ToArray();
	}

	private async Task MarkTrackedImportedAsync(int trackedDownloadId, CancellationToken cancellationToken)
	{
		var tracked = await _context.TrackedDownloads
			.FirstOrDefaultAsync(t => t.Id == trackedDownloadId, cancellationToken);

		if (tracked == null)
			return;

		tracked.Imported = true;
		await _context.SaveChangesAsync(cancellationToken);
	}

	private static QualityModel BuildQuality(ManualImportQuality? overrideQuality, BaseRelease? parsed)
	{
		if (overrideQuality == null)
			return parsed?.Quality ?? new QualityModel(new QualityResolutionModel(), new Revision());

		QualitySource? source = null;

		if (!string.IsNullOrWhiteSpace(overrideQuality.Source))
		{
			if (!Enum.TryParse<QualitySource>(overrideQuality.Source, true, out var parsedSource))
				throw new BadRequestException($"invalid quality source '{overrideQuality.Source}'");

			source = parsedSource;
		}

		QualityResolution? resolution = null;

		if (!string.IsNullOrWhiteSpace(overrideQuality.Resolution))
		{
			if (!Enum.TryParse<QualityResolution>(overrideQuality.Resolution, true, out var parsedResolution))
				throw new BadRequestException($"invalid quality resolution '{overrideQuality.Resolution}'");

			resolution = parsedResolution;
		}

		return new QualityModel(new QualityResolutionModel(source, resolution), new Revision());
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

	private static string Normalize(string value)
		=> new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

	private static List<string> CollectVideoFiles(string path)
	{
		IEnumerable<string> files = File.Exists(path)
			? new[] { path }
			: Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);

		return files
			.Where(f => MediaFileConstants.MediaFileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
			.Where(f => !Path.GetFileName(f).Contains("sample", StringComparison.OrdinalIgnoreCase))
			.OrderBy(f => f, StringComparer.Ordinal)
			.ToList();
	}

	private sealed record ManualImportPlan(
		ManualImportFile File,
		Series? Series,
		Movie? Movie,
		MediaVersion Version,
		List<Episode> Episodes,
		QualityModel Quality,
		List<Language> Languages,
		string? ReleaseGroup,
		string? Edition);
}
