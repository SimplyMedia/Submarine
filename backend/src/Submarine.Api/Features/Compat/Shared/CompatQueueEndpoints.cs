using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Queue;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Sonarr and Radarr queue routes backed by the native tracked-download queue.</summary>
public sealed class CompatQueueEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr", true);
		MapFacade(endpoints, "radarr", false);
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade, bool seriesFacade)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		group.MapGet("/queue", (HttpRequest request, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
			=> ListAsync(seriesFacade, CompatPageRequest.FromQuery(request.Query), db, selection, ct));
		group.MapGet("/queue/details", (HttpRequest request, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
			=> DetailsAsync(seriesFacade, request.Query, db, selection, ct));
		group.MapGet("/queue/status", (SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
			=> StatusAsync(seriesFacade, db, selection, ct));
		group.MapDelete("/queue/bulk", ([FromBody] BulkQueueDeleteRequest request, SubmarineDbContext db, CompatVersionSelection selection, QueueRemovalService removal, IEventBus events, CancellationToken ct)
			=> BulkDeleteAsync(seriesFacade, request, db, selection, removal, events, ct));
		group.MapDelete("/queue/{id:int}", (int id, HttpRequest request, SubmarineDbContext db, CompatVersionSelection selection, QueueRemovalService removal, IEventBus events, CancellationToken ct)
			=> DeleteAsync(seriesFacade, id, request.Query, db, selection, removal, events, ct));
	}

	private static async Task<IResult> ListAsync(bool seriesFacade, CompatPageRequest page, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var items = await SelectedItemsAsync(seriesFacade, db, selection, ct);
		var sorted = Sort(items, page.SortKey, page.SortDirection);
		var result = page.Apply(sorted.Select(Project));
		return Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> DetailsAsync(bool seriesFacade, IQueryCollection query, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var items = await SelectedItemsAsync(seriesFacade, db, selection, ct);
		if (int.TryParse(query["seriesId"], out var seriesId))
			items = items.Where(x => x.SeriesId == seriesId).ToList();
		if (int.TryParse(query["movieId"], out var movieId))
			items = items.Where(x => x.MovieId == movieId).ToList();
		if (query.TryGetValue("episodeIds", out var episodeValues))
		{
			var episodeIds = episodeValues.SelectMany(x => x?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [])
				.Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).ToHashSet();
			if (episodeIds.Count > 0)
				items = items.Where(x => x.EpisodeIds.Any(episodeIds.Contains)).ToList();
		}
		return Results.Json(items.Select(Project).ToArray(), CompatJson.Options);
	}

	private static async Task<IResult> StatusAsync(bool seriesFacade, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var items = await SelectedItemsAsync(seriesFacade, db, selection, ct);
		return Results.Json(new
		{
			totalCount = items.Count,
			count = items.Count,
			unknownCount = items.Count(x => x.SeriesId is null && x.MovieId is null),
			errors = items.Count(x => x.Status == TrackedDownloadStatus.FAILED || x.State is TrackedDownloadState.FAILED or TrackedDownloadState.FAILED_PENDING),
			warnings = items.Count(x => x.Status == TrackedDownloadStatus.WARNING)
		}, CompatJson.Options);
	}

	private static async Task<IResult> DeleteAsync(bool seriesFacade, int id, IQueryCollection query, SubmarineDbContext db, CompatVersionSelection selection, QueueRemovalService removal, IEventBus events, CancellationToken ct)
	{
		var item = await db.TrackedDownloads.FirstOrDefaultAsync(x => x.Id == id, ct);
		if (item is null || !await IsSelectedAsync(seriesFacade, item, db, selection, ct))
			return CompatErrors.Message("Queue item not found.", StatusCodes.Status404NotFound);

		await removal.RemoveAsync(item, new QueueDeleteOptions(ReadFlag(query, "removeFromClient"), ReadFlag(query, "blocklist"), ReadFlag(query, "skipRedownload")), ct);
		await db.SaveChangesAsync(ct);
		await events.PublishAsync(new QueueUpdatedEvent(), ct);
		return Results.NoContent();
	}

	private static async Task<IResult> BulkDeleteAsync(bool seriesFacade, BulkQueueDeleteRequest request, SubmarineDbContext db, CompatVersionSelection selection, QueueRemovalService removal, IEventBus events, CancellationToken ct)
	{
		if (request.Ids is null || request.Ids.Count == 0 || request.Ids.Any(id => id <= 0))
			return CompatErrors.Validation("ids", "At least one positive queue id is required.");

		var ids = request.Ids.Distinct().ToArray();
		var downloads = await db.TrackedDownloads.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
		if (downloads.Count != ids.Length)
			return CompatErrors.Message("One or more queue items were not found.", StatusCodes.Status404NotFound);
		foreach (var item in downloads)
			if (!await IsSelectedAsync(seriesFacade, item, db, selection, ct))
				return CompatErrors.Message("One or more queue items were not found.", StatusCodes.Status404NotFound);

		var options = new QueueDeleteOptions(request.RemoveFromClient, request.Blocklist, request.SkipRedownload);
		foreach (var item in downloads)
			await removal.RemoveAsync(item, options, ct);
		await db.SaveChangesAsync(ct);
		await events.PublishAsync(new QueueUpdatedEvent(), ct);
		return Results.Json(new BulkQueueDeleteResult(downloads.Count), CompatJson.Options);
	}

	private static async Task<List<TrackedDownload>> SelectedItemsAsync(bool seriesFacade, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var items = await db.TrackedDownloads.AsNoTracking()
			.Include(x => x.DownloadClient).Include(x => x.Series).Include(x => x.Movie).Include(x => x.Indexer)
			.Where(x => seriesFacade ? x.SeriesId != null : x.MovieId != null)
			.OrderByDescending(x => x.Added).ThenByDescending(x => x.Id).ToListAsync(ct);
		var selected = new Dictionary<int, int>();
		foreach (var id in items.Select(x => seriesFacade ? x.SeriesId!.Value : x.MovieId!.Value).Distinct())
		{
			var binding = seriesFacade
				? await selection.GetForSeriesAsync(id, ct)
				: await selection.GetForMovieAsync(id, ct);
			if (binding?.MediaVersionId is { } versionId)
				selected[id] = versionId;
		}
		return items.Where(x => selected.TryGetValue(seriesFacade ? x.SeriesId!.Value : x.MovieId!.Value, out var versionId) && x.MediaVersionId == versionId).ToList();
	}

	private static async Task<bool> IsSelectedAsync(bool seriesFacade, TrackedDownload item, SubmarineDbContext db, CompatVersionSelection selection, CancellationToken ct)
	{
		var titleId = seriesFacade ? item.SeriesId : item.MovieId;
		if (titleId is null || item.MediaVersionId is null)
			return false;
		if (!await db.MediaVersions.AsNoTracking().AnyAsync(x => x.Id == item.MediaVersionId && (seriesFacade ? x.SeriesId == titleId : x.MovieId == titleId), ct))
			return false;
		var binding = seriesFacade ? await selection.GetForSeriesAsync(titleId.Value, ct) : await selection.GetForMovieAsync(titleId.Value, ct);
		return binding?.MediaVersionId == item.MediaVersionId;
	}

	private static IOrderedEnumerable<TrackedDownload> Sort(IEnumerable<TrackedDownload> items, string key, string direction)
	{
		var descending = direction == "descending";
		return key.ToLowerInvariant() switch
		{
			"id" => descending ? items.OrderByDescending(x => x.Id) : items.OrderBy(x => x.Id),
			"title" => descending ? items.OrderByDescending(x => x.Title).ThenBy(x => x.Id) : items.OrderBy(x => x.Title).ThenBy(x => x.Id),
			"status" => descending ? items.OrderByDescending(x => x.Status).ThenBy(x => x.Id) : items.OrderBy(x => x.Status).ThenBy(x => x.Id),
			"size" => descending ? items.OrderByDescending(x => x.Size).ThenBy(x => x.Id) : items.OrderBy(x => x.Size).ThenBy(x => x.Id),
			"sizeleft" => descending ? items.OrderByDescending(x => x.SizeLeft).ThenBy(x => x.Id) : items.OrderBy(x => x.SizeLeft).ThenBy(x => x.Id),
			"added" => descending ? items.OrderByDescending(x => x.Added).ThenByDescending(x => x.Id) : items.OrderBy(x => x.Added).ThenBy(x => x.Id),
			_ => descending ? items.OrderByDescending(x => x.Added).ThenByDescending(x => x.Id) : items.OrderBy(x => x.Added).ThenBy(x => x.Id)
		};
	}

	private static bool? ReadFlag(IQueryCollection query, string key)
		=> bool.TryParse(query[key], out var value) ? value : null;

	private static object Project(TrackedDownload item)
		=> new
		{
			id = item.Id,
			title = item.Title,
			seriesId = item.SeriesId,
			movieId = item.MovieId,
			episodeIds = item.EpisodeIds,
			episodeId = item.EpisodeIds.Count > 0 ? item.EpisodeIds[0] : (int?)null,
			series = item.Series is null ? null : new { id = item.Series.Id, title = item.Series.Title },
			movie = item.Movie is null ? null : new { id = item.Movie.Id, title = item.Movie.Title },
			quality = QualityProjection(item.Quality),
			languages = item.Languages.Select(LanguageProjection).ToArray(),
			protocol = item.Protocol.ToString().ToLowerInvariant(),
			size = item.Size,
			sizeleft = item.SizeLeft,
			timeleft = (string?)null,
			estimatedCompletionTime = (DateTime?)null,
			status = item.Status.ToString().ToLowerInvariant(),
			trackedDownloadStatus = item.State.ToString().ToLowerInvariant(),
			statusMessages = item.StatusMessages,
			indexer = item.Indexer?.Name,
			downloadClient = item.DownloadClient.Name,
			added = item.Added,
			releaseGroup = item.ReleaseGroup,
			downloadId = item.DownloadId,
			outputPath = item.OutputPath
		};
	private static object? QualityProjection(Submarine.Core.Quality.QualityModel? quality)
	{
		if (quality is null)
			return null;
		var resolution = quality.Resolution;
		var knownQualityId = Array.FindIndex(Submarine.Core.Quality.QualityResolutionModel.All, known => known.Source == resolution.Source && known.Resolution == resolution.Resolution);
		return new
		{
			quality = new
			{
				id = knownQualityId < 0 ? (int?)null : knownQualityId + 1,
				name = resolution.Name,
				source = resolution.Source?.ToString(),
				resolution = resolution.Resolution?.ToString()
			},
			revision = new { version = quality.Revision.Version, isRepack = quality.Revision.IsRepack, isProper = quality.Revision.IsProper, isReal = quality.Revision.IsReal }
		};
	}

	private static object LanguageProjection(Submarine.Core.Languages.Language language)
		=> new
		{
			id = Convert.ToInt32(language) + 1,
			name = string.Join(' ', language.ToString().ToLowerInvariant().Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]))
		};
}
