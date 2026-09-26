using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Modules;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Sonarr;

/// <summary>
///     Sonarr-shaped manual import analysis and execution over the native <see cref="IImportService" />, keyed by
///     absolute path like the native contract instead of a synthetic persisted id.
/// </summary>
public sealed class SonarrManualImportModule : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, "sonarr", "v3");
		group.MapGet("/manualimport", AnalyzeAsync);
		group.MapPost("/manualimport", ImportAsync);
	}

	private static async Task<IResult> AnalyzeAsync(
		string folder, int? seriesId, string? downloadId, bool? filterExistingFiles,
		IImportService importService, SubmarineDbContext db, CancellationToken ct)
	{
		var candidates = await importService.AnalyzeAsync(folder, seriesId, null, downloadId, ct);
		var result = new List<object>(candidates.Count);
		foreach (var candidate in candidates)
		{
			if (filterExistingFiles == true && candidate.Rejection is not null) continue;
			Submarine.Core.Entities.Series? series = candidate.SeriesId is { } sid
				? await db.Series.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid, ct) : null;
			var episodes = candidate.EpisodeIds.Count == 0 ? [] : await db.Episodes.AsNoTracking()
				.Where(x => candidate.EpisodeIds.Contains(x.Id)).ToListAsync(ct);
			result.Add(new
			{
				id = candidate.Path.GetHashCode() & 0x7FFFFFFF,
				path = candidate.Path,
				relativePath = Path.GetFileName(candidate.Path),
				folderName = folder,
				name = Path.GetFileNameWithoutExtension(candidate.Path),
				size = candidate.Size,
				seriesId = candidate.SeriesId,
				series = series is null ? null : new { id = series.Id, title = series.Title, year = series.Year },
				episodes = episodes.Select(x => new { id = x.Id, seasonNumber = x.SeasonNumber, episodeNumber = x.EpisodeNumber, title = x.Title }),
				episodeFileId = 0,
				quality = new { quality = new { id = CompatQualityMap.ToUpstreamQualityId(candidate.Quality.Resolution, "sonarr") ?? 0, name = candidate.Quality.Resolution.Name }, revision = candidate.Quality.Revision },
				languages = candidate.Languages.Select(x => new { id = CompatLanguageMap.ToUpstreamId(x, "sonarr"), name = CompatLanguageMap.Name(x) }),
				releaseGroup = candidate.ReleaseGroup,
				downloadId,
				rejections = candidate.Rejection is null ? Array.Empty<object>() : new object[] { new { reason = candidate.Rejection.ToString(), type = "permanent" } }
			});
		}

		return Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> ImportAsync(
		JsonElement body, IImportService importService, CompatVersionSelection versions, CancellationToken ct)
	{
		if (!body.TryGetProperty("files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array)
			return CompatErrors.Validation("files", "files is required");

		var importMode = SonarrModule.String(body, "importMode")?.Equals("copy", StringComparison.OrdinalIgnoreCase) == true ? ImportMode.COPY : ImportMode.MOVE;
		var selections = new List<ManualImportSelection>();
		foreach (var item in filesElement.EnumerateArray())
		{
			var path = SonarrModule.String(item, "path");
			if (path is null) return CompatErrors.Validation("files.path", "Each file requires a path");
			var seriesId = SonarrModule.Int(item, "seriesId");
			var episodeIds = SonarrModule.IntArray(item, "episodeIds");
			int? mediaVersionId = null;
			if (seriesId is { } sid)
			{
				var binding = await versions.GetForSeriesAsync(sid, ct);
				if (binding is null) return CompatErrors.Message($"Series {sid} has no selected version", StatusCodes.Status404NotFound);
				mediaVersionId = binding.MediaVersionId;
			}

			QualityModel? quality = ParseQuality(item);
			List<Language>? languages = ParseLanguages(item);
			selections.Add(new ManualImportSelection(
				path, seriesId, episodeIds, null, mediaVersionId ?? 0, quality, languages,
				SonarrModule.String(item, "releaseGroup"), SonarrModule.String(item, "downloadId")));
		}

		var summary = await importService.ImportManualAsync(selections, importMode, ct);
		return Results.Json(new
		{
			anyImported = summary.AnyImported,
			files = summary.Files.Select(x => new { path = x.SourcePath, imported = x.Imported, destinationPath = x.DestinationPath, isUpgrade = x.IsUpgrade, rejection = x.Rejection?.ToString(), message = x.Message })
		}, CompatJson.Options);
	}

	private static QualityModel? ParseQuality(JsonElement item)
	{
		if (!item.TryGetProperty("quality", out var qualityElement) || qualityElement.ValueKind != JsonValueKind.Object) return null;
		if (!qualityElement.TryGetProperty("quality", out var innerQuality) || !innerQuality.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id)) return null;
		if (!CompatQualityMap.TryGetNativeQuality(id, "sonarr", out var resolution)) return null;
		var revision = new Revision();
		if (qualityElement.TryGetProperty("revision", out var revisionElement) && revisionElement.ValueKind == JsonValueKind.Object)
		{
			var version = revisionElement.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : 1;
			var isRepack = revisionElement.TryGetProperty("isRepack", out var r) && r.ValueKind is JsonValueKind.True or JsonValueKind.False && r.GetBoolean();
			var isProper = revisionElement.TryGetProperty("real", out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False && p.GetBoolean();
			revision = new Revision(version, isRepack, isProper);
		}

		return new QualityModel(resolution, revision);
	}

	private static List<Language>? ParseLanguages(JsonElement item)
	{
		if (!item.TryGetProperty("languages", out var languagesElement) || languagesElement.ValueKind != JsonValueKind.Array) return null;
		var languages = new List<Language>();
		foreach (var entry in languagesElement.EnumerateArray())
		{
			if (entry.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id) && CompatLanguageMap.TryGetNativeLanguage(id, "sonarr", out var language))
			{
				languages.Add(language);
			}
		}

		return languages;
	}
}
