using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Modules;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Features.Compat.Shared.Realtime;
using Submarine.Api.Features.EpisodeFiles;
using Submarine.Api.Features.Series;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Sonarr;

public sealed class SonarrModule : IEndpointModule, IServiceModule
{
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<ISonarrCompatRealtimeProjector, SonarrCompatRealtimeProjector>();

	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, "sonarr", "v3");
		group.MapGet("/series", ListSeriesAsync);
		group.MapGet("/series/lookup", LookupSeriesAsync);
		group.MapGet("/series/{id:int}", GetSeriesAsync);
		group.MapPost("/series", AddSeriesAsync);
		group.MapPut("/series/{id:int}", UpdateSeriesAsync);
		group.MapDelete("/series/{id:int}", DeleteSeriesAsync);
		group.MapPut("/series/editor", SeriesEditorAsync);
		group.MapGet("/episode", EpisodesAsync);
		group.MapGet("/episode/{id:int}", EpisodeAsync);
		group.MapPut("/episode/{id:int}", UpdateEpisodeAsync);
		group.MapPut("/episode/monitor", MonitorEpisodesAsync);
		group.MapPost("/seasonpass", SeasonPassAsync);
		group.MapGet("/episodefile", EpisodeFilesAsync);
		group.MapGet("/episodefile/{id:int}", EpisodeFileAsync);
		group.MapDelete("/episodefile/{id:int}", DeleteEpisodeFileAsync);
		group.MapDelete("/episodefile/bulk", BulkDeleteEpisodeFilesAsync);
	}

	private static async Task<IResult> ListSeriesAsync(SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var rows = await db.Series.AsNoTracking().Include(x => x.Tags).Include(x => x.Seasons).Include(x => x.Versions).Include(x => x.Episodes).ThenInclude(x => x.Files)
			.OrderBy(x => x.SortTitle).ThenBy(x => x.Id).ToListAsync(ct);
		var result = new List<object>(rows.Count);
		foreach (var row in rows)
		{
			var binding = await versions.GetForSeriesAsync(row.Id, ct);
			var mediaVersionId = binding?.MediaVersionId;
			var selectedVersion = row.Versions.FirstOrDefault(v => v.Id == mediaVersionId);
			var root = selectedVersion is null ? null : await db.RootFolders.AsNoTracking().Where(x => x.Id == selectedVersion.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(ct);
			result.Add(ProjectSeries(row, mediaVersionId, root));
		}
		return Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> GetSeriesAsync(int id, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var row = await db.Series.AsNoTracking().Include(x => x.Tags).Include(x => x.Seasons).Include(x => x.Versions).Include(x => x.Episodes).ThenInclude(x => x.Files).FirstOrDefaultAsync(x => x.Id == id, ct);
		if (row is null) return CompatErrors.Message($"Series {id} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(id, ct);
		var version = binding is null ? null : await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == binding.MediaVersionId, ct);
		var rootPath = version is null ? null : await db.RootFolders.AsNoTracking().Where(x => x.Id == version.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(ct);
		return Results.Json(ProjectSeries(row, version?.Id, rootPath), CompatJson.Options);
	}

	private static async Task<IResult> LookupSeriesAsync(string? term, IMetadataClient metadata, SubmarineDbContext db, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(term)) return CompatErrors.Validation("term", "A search term is required");
		var hits = await metadata.SearchSeriesAsync(term.Trim(), MetadataProvider.TVDB, ct);
		var result = new List<object>(hits.Count);
		foreach (var hit in hits)
		{
			var existingId = hit.TvdbId is int tvdbId ? await db.Series.Where(x => x.TvdbId == tvdbId).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) : null;
			result.Add(new { title = hit.Title, sortTitle = hit.Title, seasonCount = (int?)null, totalEpisodeCount = (int?)null, status = hit.Status?.ToLowerInvariant() ?? "continuing", ended = hit.Status?.Equals("ended", StringComparison.OrdinalIgnoreCase) ?? false, overview = hit.Overview, network = (string?)null, airTime = (string?)null, images = hit.PosterUrl is null ? Array.Empty<object>() : new object[] { new { coverType = "poster", url = hit.PosterUrl, remoteUrl = hit.PosterUrl } }, remotePoster = hit.PosterUrl, seasons = Array.Empty<object>(), year = hit.Year, profileId = (int?)null, languageProfileId = (int?)null, seasonFolder = true, monitored = false, useSceneNumbering = false, runtime = (int?)null, tvdbId = hit.TvdbId, imdbId = hit.ImdbId, titleSlug = (string?)null, id = existingId ?? 0 });
		}
		return Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> AddSeriesAsync(
		HttpContext context, JsonElement body, SubmarineDbContext db, LibraryAdder adder, CompatVersionSelection versions, CancellationToken ct)
	{
		var tvdbId = Int(body, "tvdbId");
		var rootPath = String(body, "rootFolderPath");
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Path == rootPath && x.MediaKind == MediaKind.SERIES, ct);
		var requestedQuality = Int(body, "qualityProfileId");
		var legacyProfile = Int(body, "profileId");
		if (requestedQuality is not null && legacyProfile is not null && requestedQuality != legacyProfile)
			return CompatErrors.Validation("qualityProfileId", "qualityProfileId conflicts with profileId");
		var qualityProfileId = requestedQuality ?? legacyProfile;
		var profile = qualityProfileId ?? await db.QualityProfiles.OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
		var language = Int(body, "languageProfileId") ?? await db.LanguageProfiles.OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
		if (tvdbId is null || root is null || profile is null || language is null)
			return CompatErrors.Validation("tvdbId", "A valid tvdbId, rootFolderPath, quality profile and language profile are required");
		var monitorName = String(body, "monitor") ?? NestedString(body, "addOptions", "monitor");
		var monitor = MapMonitor(monitorName);
		if (monitorName is not null && monitor is null)
			return CompatErrors.Validation("addOptions.monitor", $"Unsupported monitor option '{monitorName}'");
		var seriesType = String(body, "seriesType")?.ToLowerInvariant() switch
		{
			null or "standard" => SeriesType.STANDARD,
			"daily" => SeriesType.DAILY,
			"anime" => SeriesType.ANIME,
			_ => (SeriesType?)null
		};
		if (seriesType is null) return CompatErrors.Validation("seriesType", "Unsupported series type");
		var searchForMissing = Bool(body, "searchForMissingEpisodes") ?? NestedBool(body, "addOptions", "searchForMissingEpisodes") ?? false;
		var options = new AddSeriesOptions(tvdbId, Int(body, "tmdbId"), MetadataProvider.TVDB, String(body, "title"), root.Id,
			seriesType.Value, SeriesNumbering.AIRED, Bool(body, "seasonFolder") ?? true, Bool(body, "monitored") ?? true,
			monitor ?? AddMonitorOption.ALL, Bool(body, "monitorSpecials") ?? false,
			MonitorNewItems.ALL, IntArray(body, "tags") ?? [], [new VersionOptions("main", profile.Value, language.Value, root.Id)],
			searchForMissing);
		var added = await adder.AddSeriesAsync(options, ct);
		var selected = added.Versions.OrderBy(x => x.Id).FirstOrDefault();
		if (selected is not null) await versions.BindSeriesVersionAsync(added.Id, selected.Id, ct);
		var fresh = await db.Series.AsNoTracking().Include(x => x.Tags).Include(x => x.Seasons).Include(x => x.Versions).Include(x => x.Episodes).ThenInclude(x => x.Files).FirstAsync(x => x.Id == added.Id, ct);
		return Results.Created($"{context.Request.PathBase}/compat/sonarr/api/v3/series/{added.Id}", ProjectSeries(fresh, selected?.Id, root.Path));
	}

	private static async Task<IResult> UpdateSeriesAsync(int id, JsonElement body, SubmarineDbContext db, LibraryMutator mutator, CompatVersionSelection versions, CancellationToken ct)
	{
		var series = await db.Series.Include(x => x.Versions).Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id, ct);
		if (series is null) return CompatErrors.Message($"Series {id} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(id, ct);
		if (binding is null) return CompatErrors.Message($"Series {id} has no selected version", StatusCodes.Status404NotFound);
		var selected = series.Versions.Single(x => x.Id == binding.MediaVersionId);
		if (Int(body, "id") is int bodyId && bodyId != id)
			return CompatErrors.Validation("id", "The route id and body id must match");
		var seriesType = ParseSeriesType(String(body, "seriesType"));
		if (String(body, "seriesType") is not null && seriesType is null)
			return CompatErrors.Validation("seriesType", "Unsupported series type");
		var tagIds = IntArray(body, "tags");
		var update = new UpdateSeriesOptions(Bool(body, "monitored"), Bool(body, "seasonFolder"), seriesType, null, null, tagIds,
			null, false, [new UpdateVersionOptions(selected.Id, null, Int(body, "qualityProfileId"), Int(body, "languageProfileId"), null)]);
		await mutator.UpdateSeriesAsync(id, update, ct);
		var refreshed = await db.Series.AsNoTracking().Include(x => x.Tags).Include(x => x.Seasons).Include(x => x.Versions).Include(x => x.Episodes).ThenInclude(x => x.Files).FirstAsync(x => x.Id == id, ct);
		var selectedVersion = await db.MediaVersions.AsNoTracking().FirstAsync(x => x.Id == selected.Id, ct);
		var selectedRootPath = await db.RootFolders.AsNoTracking().Where(x => x.Id == selectedVersion.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(ct);
		return Results.Json(ProjectSeries(refreshed, selected.Id, selectedRootPath), CompatJson.Options);
	}

	private static async Task<IResult> DeleteSeriesAsync(int id, SubmarineDbContext db, LibraryMutator mutator, CancellationToken ct, bool deleteFiles = false, bool addImportListExclusion = false)
	{
		var exists = await db.Series.AnyAsync(x => x.Id == id, ct);
		if (!exists) return CompatErrors.Message($"Series {id} not found", StatusCodes.Status404NotFound);
		var versionCount = await db.MediaVersions.CountAsync(x => x.SeriesId == id, ct);
		if (versionCount > 1)
			return CompatErrors.Message("Deleting a selected series version while preserving sibling versions is not supported.", StatusCodes.Status409Conflict);
		await mutator.DeleteSeriesAsync(id, deleteFiles, addImportListExclusion, ct);
		return Results.Ok(new { });
	}

	private static async Task<IResult> SeriesEditorAsync(JsonElement body, SubmarineDbContext db, LibraryMutator mutator, CancellationToken ct)
	{
		var ids = IntArray(body, "seriesIds") ?? [];
		if (ids.Count == 0) return CompatErrors.Validation("seriesIds", "At least one seriesId is required");
		var rows = await db.Series.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
		if (rows.Count != ids.Distinct().Count()) return CompatErrors.Message("One or more series were not found", StatusCodes.Status404NotFound);
		await mutator.BulkUpdateSeriesAsync(new BulkUpdateSeriesOptions(
			ids, Bool(body, "monitored"), null, Bool(body, "seasonFolder"), null,
			Int(body, "qualityProfileId"), Int(body, "languageProfileId"), null, false, null), ct);
		return Results.Ok(new { });
	}

	private static async Task<IResult> EpisodesAsync(
		int? seriesId, int? seasonNumber, int[]? episodeIds, SubmarineDbContext db, CompatVersionSelection versions, VersionMonitoringService monitoring, CancellationToken ct)
	{
		var query = db.Episodes.AsNoTracking().Include(x => x.Series).AsQueryable();
		if (seriesId.HasValue) query = query.Where(x => x.SeriesId == seriesId.Value);
		if (seasonNumber.HasValue) query = query.Where(x => x.SeasonNumber == seasonNumber.Value);
		if (episodeIds is { Length: > 0 }) query = query.Where(x => episodeIds.Contains(x.Id));
		var episodes = await query.OrderBy(x => x.SeriesId).ThenBy(x => x.SeasonNumber).ThenBy(x => x.EpisodeNumber).ToListAsync(ct);
		var result = new List<object>(episodes.Count);
		foreach (var group in episodes.GroupBy(x => x.SeriesId))
		{
			var binding = await versions.GetForSeriesAsync(group.Key, ct);
			var groupIds = group.Select(x => x.Id).ToList();
			MediaVersion? version = null;
			string? rootPath = null;
			var files = new List<EpisodeFile>();
			IReadOnlyDictionary<int, bool> effective = new Dictionary<int, bool>();
			if (binding is not null)
			{
				version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == binding.MediaVersionId, ct);
				rootPath = version is null ? null : await db.RootFolders.AsNoTracking().Where(x => x.Id == version.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(ct);
				files = await db.EpisodeFiles.AsNoTracking().Include(x => x.Episodes)
					.Where(x => x.MediaVersionId == binding.MediaVersionId && x.Episodes.Any(e => groupIds.Contains(e.Id))).ToListAsync(ct);
				effective = await monitoring.GetEffectiveMonitoringAsync(groupIds, binding.MediaVersionId!.Value, ct);
			}

			foreach (var episode in group)
			{
				var file = files.FirstOrDefault(f => f.Episodes.Any(e => e.Id == episode.Id));
				var monitored = effective.TryGetValue(episode.Id, out var value) ? value : episode.Monitored && episode.Series.Monitored;
				result.Add(ProjectEpisode(episode, monitored, file, version, rootPath));
			}
		}

		return Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> EpisodeAsync(int id, SubmarineDbContext db, CompatVersionSelection versions, VersionMonitoringService monitoring, CancellationToken ct)
	{
		var episode = await db.Episodes.AsNoTracking().Include(x => x.Series).FirstOrDefaultAsync(x => x.Id == id, ct);
		if (episode is null) return CompatErrors.Message($"Episode {id} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(episode.SeriesId, ct);
		MediaVersion? version = null;
		string? rootPath = null;
		EpisodeFile? file = null;
		var monitored = episode.Monitored && episode.Series.Monitored;
		if (binding is not null)
		{
			version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == binding.MediaVersionId, ct);
			rootPath = version is null ? null : await db.RootFolders.AsNoTracking().Where(x => x.Id == version.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(ct);
			file = await db.EpisodeFiles.AsNoTracking().Include(x => x.Episodes)
				.Where(x => x.MediaVersionId == binding.MediaVersionId && x.Episodes.Any(e => e.Id == id)).FirstOrDefaultAsync(ct);
			monitored = await monitoring.IsEpisodeMonitoredAsync(id, binding.MediaVersionId!.Value, ct);
		}

		return Results.Json(ProjectEpisode(episode, monitored, file, version, rootPath), CompatJson.Options);
	}

	private static async Task<IResult> UpdateEpisodeAsync(int id, JsonElement body, SubmarineDbContext db, CompatVersionSelection versions, VersionMonitoringService monitoring, CancellationToken ct)
	{
		if (Bool(body, "monitored") is not bool monitored) return CompatErrors.Validation("monitored", "monitored is required");
		var episode = await db.Episodes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
		if (episode is null) return CompatErrors.Message($"Episode {id} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(episode.SeriesId, ct);
		if (binding is null) return CompatErrors.Message($"Series {episode.SeriesId} has no selected version", StatusCodes.Status404NotFound);
		await monitoring.SetEpisodeOverrideAsync(binding.MediaVersionId!.Value, id, monitored, ct);
		return Results.Ok(new { id, monitored });
	}

	private static async Task<IResult> MonitorEpisodesAsync(JsonElement body, SubmarineDbContext db, CompatVersionSelection versions, VersionMonitoringService monitoring, CancellationToken ct)
	{
		var ids = IntArray(body, "episodeIds") ?? [];
		if (ids.Count == 0 || Bool(body, "monitored") is not bool monitored) return CompatErrors.Validation("episodeIds", "episodeIds and monitored are required");
		var episodes = await db.Episodes.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.SeriesId }).ToListAsync(ct);
		if (episodes.Count != ids.Distinct().Count()) return CompatErrors.Message("One or more episodes were not found", StatusCodes.Status404NotFound);
		foreach (var group in episodes.GroupBy(x => x.SeriesId))
		{
			var binding = await versions.GetForSeriesAsync(group.Key, ct);
			if (binding is null) return CompatErrors.Message($"Series {group.Key} has no selected version", StatusCodes.Status404NotFound);
			await monitoring.SetEpisodesOverrideAsync(binding.MediaVersionId!.Value, [.. group.Select(x => x.Id)], monitored, ct);
		}

		return Results.Ok(new { });
	}

	private static async Task<IResult> SeasonPassAsync(JsonElement body, SubmarineDbContext db, CompatVersionSelection versions, VersionMonitoringService monitoring, CancellationToken ct)
	{
		var seriesId = Int(body, "seriesId");
		var monitored = Bool(body, "monitored");
		var seasonNumbers = IntArray(body, "seasonNumbers");
		if (seriesId is null || monitored is null) return CompatErrors.Validation("seriesId", "seriesId and monitored are required");
		if (!await db.Series.AnyAsync(x => x.Id == seriesId, ct))
			return CompatErrors.Message($"Series {seriesId} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(seriesId.Value, ct);
		if (binding is null) return CompatErrors.Message($"Series {seriesId} has no selected version", StatusCodes.Status404NotFound);
		var seasons = seasonNumbers ?? await db.Seasons.AsNoTracking().Where(x => x.SeriesId == seriesId).Select(x => x.SeasonNumber).ToListAsync(ct);
		foreach (var seasonNumber in seasons)
		{
			await monitoring.SetSeasonOverrideAsync(binding.MediaVersionId!.Value, seasonNumber, monitored.Value, ct);
		}

		return Results.Ok(new { });
	}

	private static async Task<IResult> EpisodeFilesAsync(int? seriesId, int[]? episodeFileIds, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var query = db.EpisodeFiles.AsNoTracking().Include(x => x.Episodes).AsQueryable();
		if (seriesId.HasValue) query = query.Where(x => x.SeriesId == seriesId.Value);
		if (episodeFileIds is { Length: > 0 }) query = query.Where(x => episodeFileIds.Contains(x.Id));
		var files = await query.OrderByDescending(x => x.DateAdded).ToListAsync(ct);
		var projected = new List<object>();
		foreach (var file in files)
		{
			var binding = await versions.GetForSeriesAsync(file.SeriesId, ct);
			if (binding?.MediaVersionId != file.MediaVersionId) continue;
			var version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == file.MediaVersionId, ct);
			var rootPath = version is null ? null : await db.RootFolders.AsNoTracking().Where(x => x.Id == version.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(ct);
			projected.Add(ProjectFile(file, version, rootPath));
		}
		return Results.Json(projected, CompatJson.Options);
	}

	private static async Task<IResult> EpisodeFileAsync(int id, SubmarineDbContext db, CompatVersionSelection versions, CancellationToken ct)
	{
		var file = await db.EpisodeFiles.AsNoTracking().Include(x => x.Episodes).FirstOrDefaultAsync(x => x.Id == id, ct);
		if (file is null) return CompatErrors.Message($"Episode file {id} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(file.SeriesId, ct);
		if (binding?.MediaVersionId != file.MediaVersionId) return CompatErrors.Message($"Episode file {id} not found", StatusCodes.Status404NotFound);
		var version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == file.MediaVersionId, ct);
		var rootPath = version is null ? null : await db.RootFolders.AsNoTracking().Where(x => x.Id == version.RootFolderId).Select(x => x.Path).FirstOrDefaultAsync(ct);
		return Results.Json(ProjectFile(file, version, rootPath), CompatJson.Options);
	}

	private static async Task<IResult> DeleteEpisodeFileAsync(
		int id, SubmarineDbContext db, CompatVersionSelection versions, EpisodeFileDeletionService deletionService, CancellationToken ct)
	{
		var file = await db.EpisodeFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
		if (file is null) return CompatErrors.Message($"Episode file {id} not found", StatusCodes.Status404NotFound);
		var binding = await versions.GetForSeriesAsync(file.SeriesId, ct);
		if (binding?.MediaVersionId != file.MediaVersionId) return CompatErrors.Message($"Episode file {id} not found", StatusCodes.Status404NotFound);
		await deletionService.DeleteAsync(id, ct);
		return Results.Ok(new { });
	}

	private static async Task<IResult> BulkDeleteEpisodeFilesAsync(
		[FromBody] JsonElement body, SubmarineDbContext db, CompatVersionSelection versions, EpisodeFileDeletionService deletionService, CancellationToken ct)
	{
		var ids = IntArray(body, "episodeFileIds") ?? [];
		if (ids.Count == 0) return CompatErrors.Validation("episodeFileIds", "At least one episodeFileId is required");
		var files = await db.EpisodeFiles.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
		if (files.Count != ids.Distinct().Count())
			return CompatErrors.Message("One or more episode files were not found", StatusCodes.Status404NotFound);
		foreach (var file in files)
		{
			var binding = await versions.GetForSeriesAsync(file.SeriesId, ct);
			if (binding?.MediaVersionId != file.MediaVersionId)
				return CompatErrors.Message($"Episode file {file.Id} does not belong to the selected version.", StatusCodes.Status409Conflict);
		}

		await deletionService.DeleteManyAsync(ids, ct);
		return Results.Ok(new { });
	}

	private static object ProjectSeries(Submarine.Core.Entities.Series series, int? selectedVersionId, string? rootPath)
	{
		var version = series.Versions.FirstOrDefault(x => x.Id == selectedVersionId);
		var path = version is null || rootPath is null ? null : Path.GetFullPath(Path.Combine(rootPath, version.Path));
		var files = series.Episodes.SelectMany(x => x.Files).Where(x => x.MediaVersionId == selectedVersionId).ToList();
		return new { id = series.Id, title = series.Title, alternateTitles = Array.Empty<object>(), sortTitle = series.SortTitle, seasonCount = series.Seasons.Count,
			 totalEpisodeCount = series.Episodes.Count, episodeCount = series.Episodes.Count(x => x.Monitored), episodeFileCount = files.Count, sizeOnDisk = files.Sum(x => x.Size),
			 status = series.Status.ToString().ToLowerInvariant(), ended = series.Status == SeriesStatus.ENDED, overview = series.Overview, network = series.Network, airTime = (string?)null,
			 images = series.PosterUrl is null ? Array.Empty<object>() : new object[] { new { coverType = "poster", url = series.PosterUrl, remoteUrl = series.PosterUrl } }, remotePoster = series.PosterUrl,
			 seasons = series.Seasons.OrderBy(x => x.SeasonNumber).Select(x => new { seasonNumber = x.SeasonNumber, monitored = x.Monitored, statistics = new { episodeCount = series.Episodes.Count(e => e.SeasonNumber == x.SeasonNumber), episodeFileCount = files.Count(f => f.Episodes.Any(e => e.SeasonNumber == x.SeasonNumber)), totalEpisodeCount = series.Episodes.Count(e => e.SeasonNumber == x.SeasonNumber), sizeOnDisk = files.Where(f => f.Episodes.Any(e => e.SeasonNumber == x.SeasonNumber)).Sum(f => f.Size), percentOfEpisodes = 0 } }),
			 year = series.Year, profileId = version?.QualityProfileId, languageProfileId = version?.LanguageProfileId, seasonFolder = series.SeasonFolder, monitored = series.Monitored, useSceneNumbering = false,
			 runtime = series.Runtime, tvdbId = series.TvdbId, tvRageId = 0, tvMazeId = 0, imdbId = series.ImdbId, titleSlug = series.CleanTitle, path, rootFolderPath = rootPath,
			 qualityProfileId = version?.QualityProfileId, seriesType = series.Type.ToString().ToLowerInvariant(), cleanTitle = series.CleanTitle, monitoredInfo = new { }, tags = series.Tags.Select(x => x.Id), added = series.CreatedAt, addOptions = new { }, statistics = new { episodeFileCount = files.Count, episodeCount = series.Episodes.Count(x => x.Monitored), totalEpisodeCount = series.Episodes.Count, sizeOnDisk = files.Sum(x => x.Size), percentOfEpisodes = 0 } };
	}

	private static object ProjectEpisode(Episode episode, bool monitored, EpisodeFile? file, MediaVersion? version, string? rootPath)
		=> new { id = episode.Id, seriesId = episode.SeriesId, tvdbId = episode.TvdbId, episodeFileId = file?.Id ?? 0, seasonNumber = episode.SeasonNumber,
			episodeNumber = episode.EpisodeNumber, title = episode.Title, airDate = episode.AirDate, airDateUtc = episode.AirDateUtc, overview = episode.Overview, hasFile = file is not null,
			monitored, absoluteEpisodeNumber = episode.AbsoluteEpisodeNumber, sceneAbsoluteEpisodeNumber = episode.SceneAbsoluteEpisodeNumber, sceneEpisodeNumber = episode.SceneEpisodeNumber,
			sceneSeasonNumber = episode.SceneSeasonNumber, unverifiedSceneNumbering = false, series = new { id = episode.Series.Id, title = episode.Series.Title, year = episode.Series.Year },
			images = Array.Empty<object>(), episodeFile = file is null ? null : ProjectFile(file, version, rootPath) };

	private static object ProjectFile(EpisodeFile file, MediaVersion? version = null, string? rootPath = null)
		=> new { id = file.Id, seriesId = file.SeriesId, seasonNumber = file.Episodes.FirstOrDefault()?.SeasonNumber ?? 0, relativePath = file.RelativePath,
			 path = version is null || rootPath is null ? null : Path.GetFullPath(Path.Combine(rootPath, version.Path, file.RelativePath)),
			 size = file.Size, dateAdded = file.DateAdded, quality = new { quality = new { id = file.Quality.Resolution.Source, name = file.Quality.Resolution.Source.ToString() }, revision = file.Quality.Revision },
			 languages = file.Languages.Select(x => new { id = (int)x, name = x.ToString() }), releaseGroup = file.ReleaseGroup, sceneName = file.SceneName, mediaInfo = file.MediaInfo, originalFilePath = file.RelativePath, qualityCutoffNotMet = false };

	internal static int? Int(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;
	internal static string? String(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	internal static bool? Bool(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
	internal static List<int>? IntArray(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(x => x.GetInt32()).ToList() : null;
	private static string? NestedString(JsonElement element, string parent, string name)
		=> element.TryGetProperty(parent, out var nested) && nested.ValueKind == JsonValueKind.Object ? String(nested, name) : null;
	private static bool? NestedBool(JsonElement element, string parent, string name)
		=> element.TryGetProperty(parent, out var nested) && nested.ValueKind == JsonValueKind.Object ? Bool(nested, name) : null;
	private static SeriesType? ParseSeriesType(string? value) => value?.ToLowerInvariant() switch
	{
		null => null,
		"standard" => SeriesType.STANDARD,
		"daily" => SeriesType.DAILY,
		"anime" => SeriesType.ANIME,
		_ => null
	};
	private static AddMonitorOption? MapMonitor(string? value) => value?.ToLowerInvariant() switch
	{
		null => null,
		"all" => AddMonitorOption.ALL,
		"none" => AddMonitorOption.NONE,
		"future" => AddMonitorOption.FUTURE,
		"missing" => AddMonitorOption.MISSING,
		"existing" => AddMonitorOption.EXISTING,
		"pilot" => AddMonitorOption.PILOT,
		"firstseason" or "first_season" => AddMonitorOption.FIRST_SEASON,
		"latestseason" or "latest_season" => AddMonitorOption.LATEST_SEASON,
		"recent" => AddMonitorOption.RECENT,
		"skip" => AddMonitorOption.SKIP,
		_ => null
	};
}
