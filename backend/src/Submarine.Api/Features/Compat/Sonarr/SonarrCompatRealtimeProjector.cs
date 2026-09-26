using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Features.Compat.Shared.Realtime;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Sonarr;

public sealed class SonarrCompatRealtimeProjector(SubmarineDbContext db, CompatVersionSelection selection, VersionMonitoringService monitoring) : ISonarrCompatRealtimeProjector
{
	public async Task<object?> ProjectSeriesAsync(int seriesId, CancellationToken cancellationToken)
	{
		var series = await db.Series.AsNoTracking().Include(x => x.Seasons).Include(x => x.Episodes).ThenInclude(x => x.Files)
			.FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken);
		if (series is null) return null;
		var binding = await selection.GetForSeriesAsync(seriesId, cancellationToken);
		var version = binding is null ? null : await db.MediaVersions.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == binding.MediaVersionId, cancellationToken);
		var rootPath = version is null ? null : await db.RootFolders.AsNoTracking().Where(x => x.Id == version.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(cancellationToken);
		var files = series.Episodes.SelectMany(x => x.Files).Where(x => binding is not null && x.MediaVersionId == binding.MediaVersionId).ToList();
		return new { id = series.Id, title = series.Title, year = series.Year, tvdbId = series.TvdbId, imdbId = series.ImdbId,
			status = series.Status.ToString().ToLowerInvariant(), monitored = series.Monitored, path = version is null || rootPath is null ? null : Path.GetFullPath(Path.Combine(rootPath, version.Path)),
			episodesChanged = true, statistics = new { episodeFileCount = files.Count, totalEpisodeCount = series.Episodes.Count, sizeOnDisk = files.Sum(x => x.Size) } };
	}

	public async Task<object?> ProjectEpisodeAsync(int episodeId, CancellationToken cancellationToken)
	{
		var episode = await db.Episodes.AsNoTracking().Include(x => x.Series).Include(x => x.Files)
			.FirstOrDefaultAsync(x => x.Id == episodeId, cancellationToken);
		if (episode is null) return null;
		var binding = await selection.GetForSeriesAsync(episode.SeriesId, cancellationToken);
		var file = episode.Files.Where(x => binding is not null && x.MediaVersionId == binding.MediaVersionId)
			.OrderByDescending(x => x.DateAdded).ThenByDescending(x => x.Id).FirstOrDefault();
		string? path = null;
		var monitored = episode.Monitored && episode.Series.Monitored;
		if (binding is not null)
		{
			monitored = await monitoring.IsEpisodeMonitoredAsync(episodeId, binding.MediaVersionId!.Value, cancellationToken);
			if (file is not null)
			{
				var version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == binding.MediaVersionId, cancellationToken);
				if (version is not null)
				{
					var rootPath = await db.RootFolders.AsNoTracking().Where(x => x.Id == version.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(cancellationToken);
					if (rootPath is not null) path = Path.GetFullPath(Path.Combine(rootPath, version.Path, file.RelativePath));
				}
			}
		}

		return new { id = episode.Id, seriesId = episode.SeriesId, title = episode.Title, seasonNumber = episode.SeasonNumber,
			episodeNumber = episode.EpisodeNumber, absoluteEpisodeNumber = episode.AbsoluteEpisodeNumber, airDate = episode.AirDate,
			airDateUtc = episode.AirDateUtc, monitored, hasFile = file is not null, episodeFileId = file?.Id,
			series = new { id = episode.Series.Id, title = episode.Series.Title, year = episode.Series.Year }, episodeFile = file is null ? null : new { id = file.Id, path, relativePath = file.RelativePath, size = file.Size } };
	}
}
