using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Response;
using Submarine.Core.Config;
using Submarine.Core.History;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.MediaFile.Naming;

namespace Submarine.Api.Services;

/// <summary>
///     Renames episode files to match the current metadata and naming templates
/// </summary>
public class RenameService
{
	private readonly SubmarineDatabaseContext _context;
	private readonly SettingsService _settingsService;
	private readonly MediaNamingService _naming;
	private readonly HistoryService _historyService;
	private readonly IBackgroundTaskQueue _taskQueue;

	public RenameService(SubmarineDatabaseContext context, SettingsService settingsService, MediaNamingService naming,
		HistoryService historyService, IBackgroundTaskQueue taskQueue)
	{
		_context = context;
		_settingsService = settingsService;
		_naming = naming;
		_historyService = historyService;
		_taskQueue = taskQueue;
	}

	public async Task RenameEpisodeFileAsync(int episodeFileId, CancellationToken cancellationToken = default)
	{
		var file = await _context.EpisodeFiles.FirstOrDefaultAsync(f => f.Id == episodeFileId, cancellationToken);

		if (file == null)
			return;

		var series = await _context.Series.FirstOrDefaultAsync(s => s.Id == file.SeriesId, cancellationToken);

		if (series == null)
			return;

		var episodes = await _context.Episodes
			.Where(e => e.EpisodeFileId == episodeFileId)
			.ToListAsync(cancellationToken);

		if (episodes.Count == 0)
			return;

		var config = await _settingsService.GetNamingConfigAsync();
		var (newRelativePath, rendered) = RenderTarget(series, file, episodes, config);

		if (string.Equals(newRelativePath, file.RelativePath, StringComparison.Ordinal))
			return;

		var currentPath = Path.Combine(series.Path, file.RelativePath);
		var destination = Path.Combine(series.Path, newRelativePath);

		var directory = Path.GetDirectoryName(destination);

		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		if (File.Exists(currentPath))
			File.Move(currentPath, destination, false);

		var oldRelativePath = file.RelativePath;
		file.RelativePath = newRelativePath;
		file.NamedFromPlaceholder = rendered.UsedPlaceholderTitle;

		await _context.SaveChangesAsync(cancellationToken);

		var ordered = episodes.OrderBy(e => e.EpisodeNumber).ToList();

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.RENAMED,
			SeriesId = series.Id,
			EpisodeId = ordered.Count == 1 ? ordered[0].Id : null,
			SourceTitle = oldRelativePath,
			Quality = file.Quality,
			Languages = file.Languages,
			Data = new Dictionary<string, string> { ["newPath"] = newRelativePath }
		}, cancellationToken);
	}

	public async Task<IReadOnlyList<RenamePreviewItem>> PreviewSeriesAsync(int seriesId)
	{
		var series = await _context.Series.FirstOrDefaultAsync(s => s.Id == seriesId);

		if (series == null)
			throw new NotFoundException();

		var config = await _settingsService.GetNamingConfigAsync();
		var files = await _context.EpisodeFiles.AsNoTracking().Where(f => f.SeriesId == seriesId).ToListAsync();
		var episodesByFile = await LoadEpisodesByFileAsync(seriesId);

		var items = new List<RenamePreviewItem>();

		foreach (var file in files)
		{
			if (!episodesByFile.TryGetValue(file.Id, out var episodes) || episodes.Count == 0)
				continue;

			var (newRelativePath, _) = RenderTarget(series, file, episodes, config);

			items.Add(new RenamePreviewItem(file.Id, file.RelativePath, newRelativePath));
		}

		return items;
	}

	public async Task<int> RenameSeriesAsync(int seriesId)
	{
		var preview = await PreviewSeriesAsync(seriesId);

		var mismatched = preview
			.Where(p => !string.Equals(p.CurrentPath, p.NewPath, StringComparison.Ordinal))
			.ToList();

		foreach (var item in mismatched)
		{
			var fileId = item.EpisodeFileId;

			await _taskQueue.QueueAsync((sp, ct) =>
				sp.GetRequiredService<RenameService>().RenameEpisodeFileAsync(fileId, ct));
		}

		return mismatched.Count;
	}

	private (string RelativePath, RenderedName Rendered) RenderTarget(Series series, EpisodeFile file,
		List<Episode> episodes, NamingConfig config)
	{
		var ordered = episodes.OrderBy(e => e.EpisodeNumber).ToList();
		var rendered = _naming.RenderEpisodeFile(series, ordered, file.Quality, file.Languages, file.ReleaseGroup,
			config);

		var extension = Path.GetExtension(file.RelativePath);
		var seasonFolder = series.SeasonFolder
			? _naming.RenderSeasonFolder(series, ordered[0].SeasonNumber, config)
			: "";

		return (Path.Combine(seasonFolder, rendered.Name + extension), rendered);
	}

	private async Task<Dictionary<int, List<Episode>>> LoadEpisodesByFileAsync(int seriesId)
	{
		var episodes = await _context.Episodes.AsNoTracking()
			.Where(e => e.SeriesId == seriesId && e.EpisodeFileId != null)
			.ToListAsync();

		return episodes
			.GroupBy(e => e.EpisodeFileId!.Value)
			.ToDictionary(g => g.Key, g => g.ToList());
	}
}
