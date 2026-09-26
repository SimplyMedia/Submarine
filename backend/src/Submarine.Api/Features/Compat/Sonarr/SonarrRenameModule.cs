using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Modules;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Sonarr;

/// <summary>
///     Sonarr-shaped rename preview, scoped to the facade's selected version. Execution goes through the generic
///     compatibility command dispatcher with the upstream "RenameFiles" command.
/// </summary>
public sealed class SonarrRenameModule : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, "sonarr", "v3");
		group.MapGet("/rename", PreviewAsync);
	}

	private static async Task<IResult> PreviewAsync(
		int seriesId, SubmarineDbContext db, CompatVersionSelection versions, NamingService namingService, CancellationToken ct)
	{
		var series = await db.Series.FirstOrDefaultAsync(x => x.Id == seriesId, ct);
		if (series is null) return CompatErrors.Message($"Series {seriesId} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(seriesId, ct);
		if (binding is null) return Results.Json(Array.Empty<object>(), CompatJson.Options);

		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(ct);
		var result = new List<object>();
		var files = await db.EpisodeFiles.Include(x => x.Episodes).Include(x => x.MediaVersion)
			.Where(x => x.SeriesId == seriesId && x.MediaVersionId == binding.MediaVersionId).ToListAsync(ct);
		foreach (var file in files)
		{
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == file.MediaVersion.RootFolderId, ct);
			var episodes = file.Episodes.OrderBy(x => x.EpisodeNumber).ToList();
			if (root is null || episodes.Count == 0) continue;

			var versionFolder = Path.Combine(root.Path, file.MediaVersion.Path);
			var targetFolder = series.SeasonFolder
				? Path.Combine(versionFolder, namingService.RenderSeasonFolder(naming, series, episodes[0].SeasonNumber))
				: versionFolder;
			var extension = Path.GetExtension(file.RelativePath);
			var newRelative = Path.GetRelativePath(versionFolder, Path.Combine(targetFolder, namingService.RenderEpisodeFileName(series, episodes, file, naming) + extension));
			if (string.Equals(newRelative, file.RelativePath, StringComparison.Ordinal)) continue;

			result.Add(new
			{
				seriesId,
				seasonNumber = episodes[0].SeasonNumber,
				episodeNumbers = episodes.Select(x => x.EpisodeNumber),
				episodeFileId = file.Id,
				existingPath = file.RelativePath,
				newPath = newRelative
			});
		}

		return Results.Json(result, CompatJson.Options);
	}
}
