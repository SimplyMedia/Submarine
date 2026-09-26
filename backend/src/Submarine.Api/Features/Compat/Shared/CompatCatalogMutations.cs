using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.RootFolders;
using Submarine.Api.Features.Tags;
using Submarine.Api.Modules;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Sonarr and Radarr v3 root-folder and tag mutation routes.</summary>
public sealed class CompatCatalogMutations : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr", MediaKind.SERIES);
		MapFacade(endpoints, "radarr", MediaKind.MOVIES);
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade, MediaKind mediaKind)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		group.MapPost("/rootfolder", (JsonElement body, RootFolderService service, CancellationToken ct) => CreateRootAsync(body, mediaKind, service, ct));
		group.MapDelete("/rootfolder/{id:int}", (int id, RootFolderService service, CancellationToken ct) => DeleteRootAsync(id, mediaKind, service, ct));
		group.MapPost("/tag", (JsonElement body, TagService service, CancellationToken ct) => CreateTagAsync(body, service, ct));
		group.MapPut("/tag", (JsonElement body, TagService service, CancellationToken ct) => UpdateTagsAsync(body, service, ct));
		group.MapPut("/tag/{id:int}", (int id, JsonElement body, TagService service, CancellationToken ct) => UpdateTagAsync(id, body, service, ct));
		group.MapDelete("/tag/{id:int}", (int id, SubmarineDbContext db, TagService service, CancellationToken ct) => DeleteTagAsync(id, mediaKind, db, service, ct));
	}

	private static async Task<IResult> CreateRootAsync(JsonElement body, MediaKind mediaKind, RootFolderService service, CancellationToken ct)
	{
		if ((HasProperty(body, "id") && (!TryInt(body, "id", out var bodyId) || bodyId != 0))
			|| !TryString(body, "path", out var path))
		{
			return Error(StatusCodes.Status400BadRequest, "A path is required and root folder ids are server assigned");
		}

		var result = await service.CreateAsync(path, mediaKind, ct);
		return result.Folder is null
			? Error(result.StatusCode, result.Error!)
			: Results.Json(RootResource(result.Folder), CompatJson.Options, statusCode: StatusCodes.Status201Created);
	}

	private static async Task<IResult> DeleteRootAsync(int id, MediaKind mediaKind, RootFolderService service, CancellationToken ct)
	{
		var existing = await service.FindAsync(id, ct);
		if (existing is null || existing.MediaKind != mediaKind)
		{
			return Error(StatusCodes.Status404NotFound, "Not Found");
		}

		var result = await service.DeleteAsync(id, ct);
		return result.Folder is null ? Error(result.StatusCode, result.Error!) : Results.NoContent();
	}

	private static async Task<IResult> CreateTagAsync(JsonElement body, TagService service, CancellationToken ct)
	{
		if ((HasProperty(body, "id") && (!TryInt(body, "id", out var bodyId) || bodyId != 0))
			|| !TryLabel(body, out var label))
		{
			return Error(StatusCodes.Status400BadRequest, "A new tag requires a label and no nonzero id");
		}

		var result = await service.CreateAsync(label, ct);
		return result.Tag is null
			? Error(result.StatusCode, result.Error!)
			: Results.Json(TagResource(result.Tag), CompatJson.Options, statusCode: StatusCodes.Status201Created);
	}

	private static async Task<IResult> UpdateTagsAsync(JsonElement body, TagService service, CancellationToken ct)
	{
		if (body.ValueKind != JsonValueKind.Array)
		{
			return Error(StatusCodes.Status400BadRequest, "An array of tags is required");
		}

		var updated = new List<object>();
		foreach (var item in body.EnumerateArray())
		{
			if (!TryInt(item, "id", out var id) || id < 0 || !TryLabel(item, out var label))
			{
				return Error(StatusCodes.Status400BadRequest, "Each tag requires a nonnegative local id and label");
			}

			var result = id == 0
				? await service.CreateAsync(label, ct)
				: await service.UpdateAsync(id, label, ct);
			if (result.Tag is null)
			{
				return Error(result.StatusCode, result.Error!);
			}

			updated.Add(TagResource(result.Tag));
		}

		return Results.Json(updated, CompatJson.Options);
	}

	private static async Task<IResult> UpdateTagAsync(int id, JsonElement body, TagService service, CancellationToken ct)
	{
		if (HasProperty(body, "id") && (!TryInt(body, "id", out var bodyId) || bodyId != id))
		{
			return Error(StatusCodes.Status400BadRequest, "Route id does not match body id");
		}

		if (!TryLabel(body, out var label))
		{
			return Error(StatusCodes.Status400BadRequest, "A valid label is required");
		}

		var result = await service.UpdateAsync(id, label, ct);
		return result.Tag is null ? Error(result.StatusCode, result.Error!) : Results.Json(TagResource(result.Tag), CompatJson.Options);
	}

	private static async Task<IResult> DeleteTagAsync(int id, MediaKind mediaKind, SubmarineDbContext db, TagService service, CancellationToken ct)
	{
		var tag = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, ct);
		if (tag is null)
		{
			return Error(StatusCodes.Status404NotFound, "Not Found");
		}

		var externalUse = mediaKind == MediaKind.SERIES
			? await db.Movies.AnyAsync(x => x.Tags.Any(t => t.Id == id), ct)
			: await db.Series.AnyAsync(x => x.Tags.Any(t => t.Id == id), ct);
		externalUse |= await db.Indexers.AnyAsync(x => x.Tags.Any(t => t.Id == id), ct)
			|| await db.Notifications.AnyAsync(x => x.Tags.Any(t => t.Id == id), ct)
			|| await db.DelayProfiles.AnyAsync(x => x.Tags.Any(t => t.Id == id), ct)
			|| await db.ReleaseProfiles.AnyAsync(x => x.Tags.Any(t => t.Id == id), ct)
			|| await db.ImportLists.AnyAsync(x => x.MediaKind != mediaKind && x.Tags.Any(t => t.Id == id), ct);
		if (externalUse)
		{
			return Error(StatusCodes.Status409Conflict, $"Tag '{tag.Label}' is still in use outside {FacadeName(mediaKind)}");
		}

		var result = await service.DeleteAsync(id, ct);
		return result.Tag is null ? Error(result.StatusCode, result.Error!) : Results.NoContent();
	}

	private static string FacadeName(MediaKind mediaKind) => mediaKind == MediaKind.SERIES ? "Sonarr" : "Radarr";

	private static object RootResource(Submarine.Core.Entities.RootFolder root)
	{
		var accessible = Directory.Exists(root.Path);
		(long? freeSpace, long? totalSpace) = (null, null);
		try
		{
			var driveRoot = Path.GetPathRoot(Path.GetFullPath(root.Path));
			if (!string.IsNullOrEmpty(driveRoot))
			{
				var drive = new DriveInfo(driveRoot);
				if (drive.IsReady)
				{
					freeSpace = drive.AvailableFreeSpace;
					totalSpace = drive.TotalSize;
				}
			}
		}
		catch (ArgumentException)
		{
		}

		return new { id = root.Id, path = root.Path, accessible, freeSpace, totalSpace, unmappedFolders = Array.Empty<string>() };
	}

	private static object TagResource(Submarine.Core.Entities.Tag tag) => new { id = tag.Id, label = tag.Label };

	private static bool HasProperty(JsonElement body, string name)
		=> body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out _);

	private static bool TryLabel(JsonElement body, out string label)
		=> TryString(body, "label", out label) && label.Length <= 256;

	private static bool TryString(JsonElement body, string name, out string value)
	{
		value = "";
		return body.ValueKind == JsonValueKind.Object
			&& body.TryGetProperty(name, out var property)
			&& property.ValueKind == JsonValueKind.String
			&& !string.IsNullOrWhiteSpace(value = property.GetString()!);
	}

	private static bool TryInt(JsonElement body, string name, out int value)
	{
		value = 0;
		return body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var property) && property.TryGetInt32(out value);
	}

	private static IResult Error(int status, string message)
		=> Results.Json(new { message }, CompatJson.Options, statusCode: status);
}
