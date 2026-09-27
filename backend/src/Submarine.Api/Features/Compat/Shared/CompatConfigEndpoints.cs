using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Config;
using Submarine.Api.Modules;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Sonarr and Radarr naming and media-management configuration routes.</summary>
public sealed class CompatConfigEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr", true);
		MapFacade(endpoints, "radarr", false);
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade, bool seriesFacade)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		MapResource(group, "/config/naming", (request, db, ct) => GetNamingAsync(seriesFacade, db, ct), (request, db, ct) => PutNamingAsync(seriesFacade, request, db, ct));
		MapResource(group, "/config/mediamanagement", (request, db, ct) => GetMediaAsync(seriesFacade, db, ct), (request, db, ct) => PutMediaAsync(seriesFacade, request, db, ct));
	}

	private static void MapResource(
		RouteGroupBuilder group,
		string route,
		Func<HttpRequest, SubmarineDbContext, CancellationToken, Task<IResult>> get,
		Func<HttpRequest, SubmarineDbContext, CancellationToken, Task<IResult>> put)
	{
		group.MapGet(route, (HttpRequest request, SubmarineDbContext db, CancellationToken ct) => get(request, db, ct));
		group.MapPut(route, (HttpRequest request, SubmarineDbContext db, CancellationToken ct) => put(request, db, ct));
		group.MapPut(route + "/{id:int}", (int id, HttpRequest request, SubmarineDbContext db, CancellationToken ct) =>
			id == 1 ? put(request, db, ct) : Task.FromResult(Error("Configuration not found.", StatusCodes.Status404NotFound)));
	}

	private static async Task<IResult> GetNamingAsync(bool seriesFacade, SubmarineDbContext db, CancellationToken ct)
	{
		var config = await db.NamingConfig.AsNoTracking().SingleAsync(ct);
		return Results.Json(NamingRecord(NamingConfigResource.FromEntity(config), seriesFacade), CompatJson.Options);
	}

	private static async Task<IResult> PutNamingAsync(bool seriesFacade, HttpRequest request, SubmarineDbContext db, CancellationToken ct)
	{
		var parsed = await ReadObjectAsync(request, ct);
		if (parsed.Document is null) return parsed.Error!;
		using var document = parsed.Document;
		var current = NamingConfigResource.FromEntity(await db.NamingConfig.AsNoTracking().SingleAsync(ct));
		if (!TryNamingUpdate(document.RootElement, seriesFacade, current, out var updated, out var message))
			return Error(message!, StatusCodes.Status400BadRequest);
		var saved = await NamingConfigService.UpdateAsync(db, updated!, ct);
		return Results.Json(NamingRecord(saved, seriesFacade), CompatJson.Options);
	}

	private static object NamingRecord(NamingConfigResource config, bool seriesFacade)
		=> seriesFacade
			? new { id = 1, renameEpisodes = config.RenameEpisodes, replaceIllegalCharacters = config.ReplaceIllegalCharacters, colonReplacement = (int)config.ColonReplacement, standardEpisodeFormat = config.StandardEpisodeFormat, dailyEpisodeFormat = config.DailyEpisodeFormat, animeEpisodeFormat = config.AnimeEpisodeFormat, seriesFolderFormat = config.SeriesFolderFormat, seasonFolderFormat = config.SeasonFolderFormat, specialsFolderFormat = config.SpecialsFolderFormat, multiEpisodeStyle = (int)config.MultiEpisodeStyle }
			: new { id = 1, renameMovies = config.RenameMovies, replaceIllegalCharacters = config.ReplaceIllegalCharacters, colonReplacement = (int)config.ColonReplacement, standardMovieFormat = config.MovieFormat, movieFolderFormat = config.MovieFolderFormat, multiEpisodeStyle = (int)config.MultiEpisodeStyle };

	private static bool TryNamingUpdate(JsonElement body, bool seriesFacade, NamingConfigResource current, out NamingConfigResource? updated, out string? error)
	{
		var config = current;
		var valid = TryBoolean(body, "replaceIllegalCharacters", value => config = config with { ReplaceIllegalCharacters = value }, out error)
			&& TryEnum(body, "colonReplacement", 4, value => config = config with { ColonReplacement = (ColonReplacement)value }, out error)
			&& TryEnum(body, "multiEpisodeStyle", 5, value => config = config with { MultiEpisodeStyle = (MultiEpisodeStyle)value }, out error);
		if (valid && seriesFacade)
		{
			valid = TryBoolean(body, "renameEpisodes", value => config = config with { RenameEpisodes = value }, out error)
				&& TryString(body, "standardEpisodeFormat", value => config = config with { StandardEpisodeFormat = value }, out error)
				&& TryString(body, "dailyEpisodeFormat", value => config = config with { DailyEpisodeFormat = value }, out error)
				&& TryString(body, "animeEpisodeFormat", value => config = config with { AnimeEpisodeFormat = value }, out error)
				&& TryString(body, "seriesFolderFormat", value => config = config with { SeriesFolderFormat = value }, out error)
				&& TryString(body, "seasonFolderFormat", value => config = config with { SeasonFolderFormat = value }, out error)
				&& TryString(body, "specialsFolderFormat", value => config = config with { SpecialsFolderFormat = value }, out error);
		}
		else if (valid)
		{
			valid = TryBoolean(body, "renameMovies", value => config = config with { RenameMovies = value }, out error)
				&& TryString(body, "standardMovieFormat", value => config = config with { MovieFormat = value }, out error)
				&& TryString(body, "movieFolderFormat", value => config = config with { MovieFolderFormat = value }, out error);
		}
		updated = valid ? config : null;
		return valid;
	}

	private static async Task<IResult> GetMediaAsync(bool seriesFacade, SubmarineDbContext db, CancellationToken ct)
	{
		var config = await db.MediaManagementConfig.AsNoTracking().SingleAsync(ct);
		return Results.Json(MediaRecord(MediaManagementConfigResource.FromEntity(config), seriesFacade), CompatJson.Options);
	}

	private static async Task<IResult> PutMediaAsync(bool seriesFacade, HttpRequest request, SubmarineDbContext db, CancellationToken ct)
	{
		var parsed = await ReadObjectAsync(request, ct);
		if (parsed.Document is null) return parsed.Error!;
		using var document = parsed.Document;
		var current = MediaManagementConfigResource.FromEntity(await db.MediaManagementConfig.AsNoTracking().SingleAsync(ct));
		if (!TryMediaUpdate(document.RootElement, seriesFacade, current, out var updated, out var message))
			return Error(message!, StatusCodes.Status400BadRequest);
		if (updated!.MinimumFreeSpaceMb < 0 || updated.RecycleBinCleanupDays < 0 || !Enum.IsDefined(updated.DownloadPropersAndRepacks)
			|| updated.ChmodFolder.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(updated.ChmodFolder, "^[0-7]{3,4}$")
			|| updated.ChmodFile.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(updated.ChmodFile, "^[0-7]{3,4}$"))
			return Error("Media-management settings are invalid.", StatusCodes.Status400BadRequest);
		var saved = await MediaManagementConfigService.UpdateAsync(db, updated, ct);
		return Results.Json(MediaRecord(saved, seriesFacade), CompatJson.Options);
	}

	private static object MediaRecord(MediaManagementConfigResource config, bool seriesFacade)
	{
		var record = new Dictionary<string, object?>
		{
			["id"] = 1,
			["useHardlinks"] = config.UseHardlinks,
			["importExtraFiles"] = config.ImportExtraFiles,
			["extraFileExtensions"] = config.ExtraFileExtensions,
			["minimumFreeSpaceWhenImporting"] = config.MinimumFreeSpaceMb,
			["skipFreeSpaceCheck"] = config.SkipFreeSpaceCheck,
			["recycleBin"] = config.RecycleBinPath,
			["recycleBinCleanupDays"] = config.RecycleBinCleanupDays,
			["chmodFolder"] = config.ChmodFolder,
			["chmodFile"] = config.ChmodFile,
			["chownGroup"] = config.ChownGroup,
			["downloadPropersAndRepacks"] = config.DownloadPropersAndRepacks switch
			{
				DownloadPropersAndRepacks.DO_NOT_UPGRADE => 0,
				DownloadPropersAndRepacks.DO_NOT_PREFER => 1,
				_ => 2
			}
		};
		record[seriesFacade ? "autoUnmonitorPreviouslyDownloadedEpisodes" : "autoUnmonitorPreviouslyDownloadedMovies"] = config.UnmonitorDeletedFiles;
		return record;
	}

	private static bool TryMediaUpdate(JsonElement body, bool seriesFacade, MediaManagementConfigResource current, out MediaManagementConfigResource? updated, out string? error)
	{
		var config = current;
		var valid = TryBoolean(body, "useHardlinks", value => config = config with { UseHardlinks = value }, out error)
			&& TryBoolean(body, "importExtraFiles", value => config = config with { ImportExtraFiles = value }, out error)
			&& TryString(body, "extraFileExtensions", value => config = config with { ExtraFileExtensions = value }, out error)
			&& TryInteger(body, "minimumFreeSpaceWhenImporting", value => config = config with { MinimumFreeSpaceMb = value }, out error)
			&& TryBoolean(body, "skipFreeSpaceCheck", value => config = config with { SkipFreeSpaceCheck = value }, out error)
			&& TryString(body, "recycleBin", value => config = config with { RecycleBinPath = value }, out error)
			&& TryInteger(body, "recycleBinCleanupDays", value => config = config with { RecycleBinCleanupDays = value }, out error)
			&& TryString(body, "chmodFolder", value => config = config with { ChmodFolder = value }, out error)
			&& TryString(body, "chmodFile", value => config = config with { ChmodFile = value }, out error)
			&& TryString(body, "chownGroup", value => config = config with { ChownGroup = value }, out error)
			&& TryEnum(body, "downloadPropersAndRepacks", 2, value => config = config with { DownloadPropersAndRepacks = value switch { 0 => DownloadPropersAndRepacks.DO_NOT_UPGRADE, 1 => DownloadPropersAndRepacks.DO_NOT_PREFER, _ => DownloadPropersAndRepacks.PREFER_AND_UPGRADE } }, out error);
		var autoUnmonitorField = seriesFacade ? "autoUnmonitorPreviouslyDownloadedEpisodes" : "autoUnmonitorPreviouslyDownloadedMovies";
		valid = valid && TryBoolean(body, autoUnmonitorField, value => config = config with { UnmonitorDeletedFiles = value }, out error);
		updated = valid ? config : null;
		return valid;
	}

	private static async Task<(JsonDocument? Document, IResult? Error)> ReadObjectAsync(HttpRequest request, CancellationToken ct)
	{
		try
		{
			var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
			if (document.RootElement.ValueKind == JsonValueKind.Object) return (document, null);
			document.Dispose();
			return (null, Error("Configuration body must be a JSON object.", StatusCodes.Status400BadRequest));
		}
		catch (JsonException)
		{
			return (null, Error("Configuration body must be valid JSON.", StatusCodes.Status400BadRequest));
		}
	}

	private static bool TryBoolean(JsonElement body, string name, Action<bool> set, out string? error)
		=> TryValue(body, name, value => value.ValueKind is JsonValueKind.True or JsonValueKind.False, value => set(value.GetBoolean()), out error);

	private static bool TryString(JsonElement body, string name, Action<string> set, out string? error)
		=> TryValue(body, name, value => value.ValueKind is JsonValueKind.String or JsonValueKind.Null, value => set(value.ValueKind == JsonValueKind.Null ? string.Empty : value.GetString()!), out error);
	private static bool TryInteger(JsonElement body, string name, Action<int> set, out string? error)
		=> TryValue(body, name, value => value.TryGetInt32(out _), value => set(value.GetInt32()), out error);

	private static bool TryEnum(JsonElement body, string name, int maximum, Action<int> set, out string? error)
		=> TryValue(body, name, value => value.TryGetInt32(out var number) && number >= 0 && number <= maximum, value => set(value.GetInt32()), out error);

	private static bool TryValue(JsonElement body, string name, Func<JsonElement, bool> isValid, Action<JsonElement> set, out string? error)
	{
		if (!body.TryGetProperty(name, out var value)) { error = null; return true; }
		if (!isValid(value)) { error = $"Configuration field '{name}' has an invalid value."; return false; }
		set(value);
		error = null;
		return true;
	}

	private static IResult Error(string message, int statusCode)
		=> Results.Json(new { message }, CompatJson.Options, statusCode: statusCode);
}
