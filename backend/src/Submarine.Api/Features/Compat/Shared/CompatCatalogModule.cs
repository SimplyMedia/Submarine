using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Read-only catalog endpoints shared by the Sonarr and Radarr facades.</summary>
public sealed class CompatCatalogModule : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr", MediaKind.SERIES);
		MapFacade(endpoints, "radarr", MediaKind.MOVIES);
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade, MediaKind mediaKind)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		group.MapGet("/qualityprofile", (SubmarineDbContext db, CancellationToken ct) => QualityProfilesAsync(facade, db, ct));
		group.MapGet("/qualityprofile/schema", (SubmarineDbContext db, CancellationToken ct) => QualityProfileSchema(facade, db, ct));
		group.MapGet("/qualityprofile/{id:int}", (int id, SubmarineDbContext db, CancellationToken ct) => QualityProfileAsync(id, facade, db, ct));
		group.MapGet("/profile", (SubmarineDbContext db, CancellationToken ct) => QualityProfilesAsync(facade, db, ct));
		group.MapGet("/language", () => Languages(facade));
		group.MapGet("/language/{id:int}", (int id) => LanguageById(id, facade));
		group.MapGet("/rootfolder", (SubmarineDbContext db, CancellationToken ct) => RootFoldersAsync(mediaKind, db, ct));
		group.MapGet("/rootfolder/{id:int}", (int id, SubmarineDbContext db, CancellationToken ct) => RootFolderAsync(id, mediaKind, db, ct));
		group.MapGet("/tag", (SubmarineDbContext db, CancellationToken ct) => TagsAsync(db, ct));
		group.MapGet("/tag/{id:int}", (int id, SubmarineDbContext db, CancellationToken ct) => TagAsync(id, db, ct));
		group.MapGet("/tag/detail", (SubmarineDbContext db, CancellationToken ct) => TagDetailsAsync(null, mediaKind, db, ct));
		group.MapGet("/tag/detail/{id:int}", (int id, SubmarineDbContext db, CancellationToken ct) => TagDetailsAsync(id, mediaKind, db, ct));
	}

	private static async Task<IResult> QualityProfilesAsync(string facade, SubmarineDbContext db, CancellationToken ct)
		=> Results.Json((await db.QualityProfiles.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(profile => QualityProfile(profile, facade)).ToList(), CompatJson.Options);

	private static async Task<IResult> QualityProfileAsync(int id, string facade, SubmarineDbContext db, CancellationToken ct)
	{
		var profile = await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
		return profile is null
			? Results.Json(new { message = "Not Found" }, CompatJson.Options, statusCode: StatusCodes.Status404NotFound)
			: Results.Json(QualityProfile(profile, facade), CompatJson.Options);
	}

	private static async Task<IResult> QualityProfileSchema(string facade, SubmarineDbContext db, CancellationToken ct)
	{
		var profile = await db.QualityProfiles.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		return Results.Json(profile is null ? DefaultQualityProfile() : QualityProfile(profile, facade), CompatJson.Options);
	}

	private static object QualityProfile(QualityProfile profile, string facade)
		=> new
		{
			id = profile.Id,
			name = profile.Name,
			upgradeAllowed = profile.UpgradeAllowed,
			cutoff = profile.Cutoff,
			items = profile.Items.Select(item =>
			{
				var id = CompatQualityMap.ToUpstreamQualityId(item.Quality, facade);
				return new
				{
					quality = new
					{
						id,
						name = id is { } qualityId ? CompatQualityMap.ToUpstreamQualityName(qualityId, facade) : item.Quality.Name,
						source = UpstreamQualitySource(item.Quality.Source, facade),
						resolution = UpstreamQualityResolution(item.Quality.Resolution)
					},
					allowed = item.Allowed
				};
			}).ToList(),
			formatItems = profile.FormatItems.Select(item => new { format = new { id = item.CustomFormatId }, score = item.Score }).ToList(),
			minFormatScore = profile.MinFormatScore,
			cutoffFormatScore = profile.CutoffFormatScore,
			minUpgradeFormatScore = profile.MinUpgradeFormatScore
		};

	private static string? UpstreamQualitySource(Submarine.Core.Quality.QualitySource? source, string facade)
		=> (facade, source) switch
		{
			("sonarr", Submarine.Core.Quality.QualitySource.UNKNOWN) => "unknown",
			("sonarr", Submarine.Core.Quality.QualitySource.TV) => "television",
			("sonarr", Submarine.Core.Quality.QualitySource.DVD) => "dvd",
			("sonarr", Submarine.Core.Quality.QualitySource.WEB_DL) => "web",
			("sonarr", Submarine.Core.Quality.QualitySource.WEB_RIP) => "webRip",
			("sonarr", Submarine.Core.Quality.QualitySource.BLURAY) => "bluray",
			("sonarr", Submarine.Core.Quality.QualitySource.BLURAY_REMUX) => "blurayRaw",
			("sonarr", Submarine.Core.Quality.QualitySource.RAW_HD) => "televisionRaw",
			("radarr", Submarine.Core.Quality.QualitySource.UNKNOWN) => "unknown",
			("radarr", Submarine.Core.Quality.QualitySource.CAM) => "cam",
			("radarr", Submarine.Core.Quality.QualitySource.TV) => "tv",
			("radarr", Submarine.Core.Quality.QualitySource.DVD) => "dvd",
			("radarr", Submarine.Core.Quality.QualitySource.WEB_DL) => "webdl",
			("radarr", Submarine.Core.Quality.QualitySource.WEB_RIP) => "webrip",
			("radarr", Submarine.Core.Quality.QualitySource.BLURAY or Submarine.Core.Quality.QualitySource.BLURAY_REMUX or Submarine.Core.Quality.QualitySource.BLURAY_DISK) => "bluray",
			("radarr", Submarine.Core.Quality.QualitySource.RAW_HD) => "tv",
			_ => null
		};

	private static int? UpstreamQualityResolution(Submarine.Core.Quality.QualityResolution? resolution)
		=> resolution switch
		{
			Submarine.Core.Quality.QualityResolution.R480_P => 480,
			Submarine.Core.Quality.QualityResolution.R576_P => 576,
			Submarine.Core.Quality.QualityResolution.R720_P => 720,
			Submarine.Core.Quality.QualityResolution.R1080_P => 1080,
			Submarine.Core.Quality.QualityResolution.R2160_P => 2160,
			_ => null
		};

	private static object DefaultQualityProfile()
		=> new
		{
			id = 0,
			name = "",
			upgradeAllowed = true,
			cutoff = 0,
			items = Array.Empty<object>(),
			formatItems = Array.Empty<object>(),
			minFormatScore = 0,
			cutoffFormatScore = 0,
			minUpgradeFormatScore = 0
		};

	

	private static IResult Languages(string facade)
		=> Results.Json(CompatLanguageMap.Catalog(facade), CompatJson.Options);

	private static IResult LanguageById(int id, string facade)
	{
		var language = CompatLanguageMap.Catalog(facade).FirstOrDefault(value => value.Id == id);
		return language is null
			? Results.Json(new { message = "Not Found" }, CompatJson.Options, statusCode: StatusCodes.Status404NotFound)
			: Results.Json(language, CompatJson.Options);
	}

	private static async Task<IResult> RootFoldersAsync(MediaKind mediaKind, SubmarineDbContext db, CancellationToken ct)
	{
		var roots = await db.RootFolders.AsNoTracking().Where(root => root.MediaKind == mediaKind).OrderBy(root => root.Id).ToListAsync(ct);
		return Results.Json(roots.Select(RootFolder).ToList(), CompatJson.Options);
	}

	private static async Task<IResult> RootFolderAsync(int id, MediaKind mediaKind, SubmarineDbContext db, CancellationToken ct)
	{
		var root = await db.RootFolders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.MediaKind == mediaKind, ct);
		return root is null
			? Results.Json(new { message = "Not Found" }, CompatJson.Options, statusCode: StatusCodes.Status404NotFound)
			: Results.Json(RootFolder(root), CompatJson.Options);
	}

	private static object RootFolder(RootFolder root)
	{
		var accessible = Directory.Exists(root.Path);
		var (freeSpace, totalSpace) = SpaceOf(root.Path);
		return new { id = root.Id, path = root.Path, accessible, freeSpace, totalSpace, unmappedFolders = Array.Empty<string>() };
	}

	private static (long? FreeSpace, long? TotalSpace) SpaceOf(string path)
	{
		try
		{
			var driveRoot = Path.GetPathRoot(Path.GetFullPath(path));
			if (string.IsNullOrEmpty(driveRoot))
			{
				return (null, null);
			}

			var drive = new DriveInfo(driveRoot);
			return drive.IsReady ? (drive.AvailableFreeSpace, drive.TotalSize) : (null, null);
		}
		catch (ArgumentException)
		{
			return (null, null);
		}
	}

	private static async Task<IResult> TagsAsync(SubmarineDbContext db, CancellationToken ct)
		=> Results.Json(await db.Tags.AsNoTracking().OrderBy(tag => tag.Id).Select(tag => new { id = tag.Id, label = tag.Label }).ToListAsync(ct), CompatJson.Options);

	private static async Task<IResult> TagAsync(int id, SubmarineDbContext db, CancellationToken ct)
	{
		var tag = await db.Tags.AsNoTracking().Where(x => x.Id == id).Select(x => new { id = x.Id, label = x.Label }).FirstOrDefaultAsync(ct);
		return tag is null
			? Results.Json(new { message = "Not Found" }, CompatJson.Options, statusCode: StatusCodes.Status404NotFound)
			: Results.Json(tag, CompatJson.Options);
	}

	private static async Task<IResult> TagDetailsAsync(int? id, MediaKind mediaKind, SubmarineDbContext db, CancellationToken ct)
	{
		if (id is not null && !await db.Tags.AnyAsync(tag => tag.Id == id, ct))
		{
			return Results.Json(new { message = "Not Found" }, CompatJson.Options, statusCode: StatusCodes.Status404NotFound);
		}

		var ids = id is null
			? await db.Tags.AsNoTracking().OrderBy(tag => tag.Id).Select(tag => tag.Id).ToListAsync(ct)
			: [id.Value];
		var seriesFacade = mediaKind == MediaKind.SERIES;
		var details = new List<object>(ids.Count);
		foreach (var tagId in ids)
		{
			List<int> seriesIds = seriesFacade
				? await db.Series.AsNoTracking().Where(item => item.Tags.Any(tag => tag.Id == tagId)).Select(item => item.Id).ToListAsync(ct)
				: [];
			List<int> movieIds = seriesFacade
				? []
				: await db.Movies.AsNoTracking().Where(item => item.Tags.Any(tag => tag.Id == tagId)).Select(item => item.Id).ToListAsync(ct);
			var indexerIds = await db.Indexers.AsNoTracking().Where(item => item.Tags.Any(tag => tag.Id == tagId)).Select(item => item.Id).ToListAsync(ct);
			var notificationIds = await db.Notifications.AsNoTracking().Where(item => item.Tags.Any(tag => tag.Id == tagId)).Select(item => item.Id).ToListAsync(ct);
			var delayProfileIds = await db.DelayProfiles.AsNoTracking().Where(item => item.Tags.Any(tag => tag.Id == tagId)).Select(item => item.Id).ToListAsync(ct);
			var releaseProfileIds = await db.ReleaseProfiles.AsNoTracking().Where(item => item.Tags.Any(tag => tag.Id == tagId)).Select(item => item.Id).ToListAsync(ct);
			var importListIds = await db.ImportLists.AsNoTracking()
				.Where(item => item.MediaKind == mediaKind && item.Tags.Any(tag => tag.Id == tagId))
				.Select(item => item.Id).ToListAsync(ct);
			var inUse = seriesIds.Count != 0 || movieIds.Count != 0 || indexerIds.Count != 0
				|| notificationIds.Count != 0 || delayProfileIds.Count != 0 || releaseProfileIds.Count != 0 || importListIds.Count != 0;
			details.Add(new { id = tagId, seriesIds, movieIds, indexerIds, notificationIds, delayProfileIds, releaseProfileIds, importListIds, inUse });
		}

		return Results.Json(id is null ? details : details[0], CompatJson.Options);
	}
}
