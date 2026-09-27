using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.QualityProfiles;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Sonarr and Radarr quality-profile write routes backed by the native profile operation.</summary>
public sealed class CompatQualityProfileEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr");
		MapFacade(endpoints, "radarr");
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		group.MapPost("/qualityprofile", (HttpRequest request, HttpResponse response, QualityProfileService service, SubmarineDbContext db, CancellationToken ct) => CreateAsync(facade, request, response, service, db, ct));
		group.MapPut("/qualityprofile", (HttpRequest request, QualityProfileService service, SubmarineDbContext db, CancellationToken ct) => UpdateCollectionAsync(facade, request, service, db, ct));
		group.MapPut("/qualityprofile/{id:int}", (int id, HttpRequest request, QualityProfileService service, SubmarineDbContext db, CancellationToken ct) => UpdateAsync(facade, id, request, service, db, ct));
		group.MapDelete("/qualityprofile/{id:int}", (int id, QualityProfileService service, CancellationToken ct) => DeleteAsync(id, service, ct));
	}

	private static async Task<IResult> CreateAsync(string facade, HttpRequest request, HttpResponse response, QualityProfileService service, SubmarineDbContext db, CancellationToken ct)
	{
		var body = await ReadBodyAsync(request, ct);
		if (body is null || body.Value.ValueKind != JsonValueKind.Object) return CompatErrors.Validation("", "A valid quality profile object is required.");
		var objectBody = body.Value;
		if (!TryParse(objectBody, facade, null, out var profileRequest, out var error)) return CompatErrors.Validation(error.Property, error.Message);
		try
		{
			var profile = await service.CreateAsync(profileRequest, ct);
			response.Headers.Location = $"{request.PathBase}/compat/{facade}/api/v3/qualityprofile/{profile.Id}";
			return Results.Json(Project(profile, facade, db), CompatJson.Options, statusCode: StatusCodes.Status201Created);
		}
		catch (Submarine.Core.Common.ConflictException exception)
		{
			return CompatErrors.Message(exception.Message, StatusCodes.Status409Conflict);
		}
		catch (FluentValidation.ValidationException exception)
		{
			return CompatErrors.Validation(exception.Errors.FirstOrDefault()?.PropertyName ?? "", exception.Errors.FirstOrDefault()?.ErrorMessage ?? "Invalid quality profile.");
		}
	}

	private static async Task<IResult> UpdateAsync(string facade, int id, HttpRequest request, QualityProfileService service, SubmarineDbContext db, CancellationToken ct)
	{
		var body = await ReadBodyAsync(request, ct);
		if (body is null || body.Value.ValueKind != JsonValueKind.Object) return CompatErrors.Validation("", "A valid quality profile object is required.");
		var objectBody = body.Value;
		if (HasId(objectBody) && (!TryGetInt(objectBody, "id", out var bodyId) || bodyId != id))
			return CompatErrors.Validation("id", "The route id must match the quality profile id.");
		var existing = await db.QualityProfiles.FirstOrDefaultAsync(profile => profile.Id == id, ct);
		if (existing is null) return CompatErrors.Message("Quality profile not found.", StatusCodes.Status404NotFound);
		if (!TryParse(objectBody, facade, existing, out var profileRequest, out var error)) return CompatErrors.Validation(error.Property, error.Message);
		try
		{
			var profile = await service.UpdateAsync(id, profileRequest, ct);
			return profile is null
				? CompatErrors.Message("Quality profile not found.", StatusCodes.Status404NotFound)
				: Results.Json(Project(profile, facade, db), CompatJson.Options);
		}
		catch (Submarine.Core.Common.ConflictException exception)
		{
			return CompatErrors.Message(exception.Message, StatusCodes.Status409Conflict);
		}
		catch (FluentValidation.ValidationException exception)
		{
			return CompatErrors.Validation(exception.Errors.FirstOrDefault()?.PropertyName ?? "", exception.Errors.FirstOrDefault()?.ErrorMessage ?? "Invalid quality profile.");
		}
	}

	private static async Task<IResult> UpdateCollectionAsync(string facade, HttpRequest request, QualityProfileService service, SubmarineDbContext db, CancellationToken ct)
	{
		var body = await ReadBodyAsync(request, ct);
		if (body is null || body.Value.ValueKind != JsonValueKind.Array) return CompatErrors.Validation("", "A quality profile array is required.");
		var arrayBody = body.Value;
		var profiles = await db.QualityProfiles.ToDictionaryAsync(profile => profile.Id, ct);
		var updates = new List<(int Id, QualityProfileRequest Request)>();
		foreach (var item in arrayBody.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object || !TryGetInt(item, "id", out var id))
				return CompatErrors.Validation("id", "Each quality profile requires an id.");
			if (!profiles.TryGetValue(id, out var existing)) return CompatErrors.Message($"Quality profile {id} not found.", StatusCodes.Status404NotFound);
			if (!TryParse(item, facade, existing, out var profileRequest, out var error)) return CompatErrors.Validation(error.Property, error.Message);
			updates.Add((id, profileRequest));
		}

		await using var transaction = await db.Database.BeginTransactionAsync(ct);
		try
		{
			var updated = new List<QualityProfile>(updates.Count);
			foreach (var (id, profileRequest) in updates)
				updated.Add(await service.UpdateAsync(id, profileRequest, ct) ?? throw new KeyNotFoundException());
			await transaction.CommitAsync(ct);
			return Results.Json(updated.Select(profile => Project(profile, facade, db)).ToArray(), CompatJson.Options);
		}
		catch (Submarine.Core.Common.ConflictException exception)
		{
			await transaction.RollbackAsync(ct);
			return CompatErrors.Message(exception.Message, StatusCodes.Status409Conflict);
		}
		catch (FluentValidation.ValidationException exception)
		{
			await transaction.RollbackAsync(ct);
			return CompatErrors.Validation(exception.Errors.FirstOrDefault()?.PropertyName ?? "", exception.Errors.FirstOrDefault()?.ErrorMessage ?? "Invalid quality profile.");
		}
	}

	private static async Task<IResult> DeleteAsync(int id, QualityProfileService service, CancellationToken ct)
	{
		try
		{
			return await service.DeleteAsync(id, ct)
				? Results.Ok()
				: CompatErrors.Message("Quality profile not found.", StatusCodes.Status404NotFound);
		}
		catch (Submarine.Core.Common.ConflictException exception)
		{
			return CompatErrors.Message(exception.Message, StatusCodes.Status409Conflict);
		}
	}

	private static bool TryParse(JsonElement body, string facade, QualityProfile? current, out QualityProfileRequest request, out (string Property, string Message) error)
	{
		if (body.TryGetProperty("upgradeAllowed", out var upgradeElement) && upgradeElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
			return Fail("upgradeAllowed", "upgradeAllowed must be a boolean.", out request, out error);
		foreach (var scoreName in new[] { "minFormatScore", "cutoffFormatScore", "minUpgradeFormatScore" })
			if (body.TryGetProperty(scoreName, out var scoreElement) && (scoreElement.ValueKind != JsonValueKind.Number || !scoreElement.TryGetInt32(out _)))
				return Fail(scoreName, $"{scoreName} must be an integer.", out request, out error);
		var name = ReadString(body, "name", current?.Name);
		if (string.IsNullOrWhiteSpace(name)) return Fail("name", "A profile name is required.", out request, out error);
		var upgradeAllowed = ReadBool(body, "upgradeAllowed", current?.UpgradeAllowed ?? true);
		var items = current is null ? [] : current.Items.Select(item => new QualityProfileItemRequest(
			new QualityModelResource(item.Quality.Source?.ToString(), item.Quality.Resolution?.ToString(), item.Quality.Name), item.Allowed)).ToList();
		var upstreamIds = current is null ? [] : current.Items.Select(item => CompatQualityMap.ToUpstreamQualityId(item.Quality, facade) ?? -1).ToList();

		if (body.TryGetProperty("items", out var itemsElement))
		{
			if (itemsElement.ValueKind != JsonValueKind.Array) return Fail("items", "Items must be an array.", out request, out error);
			items = [];
			upstreamIds = [];
			var index = 0;
			foreach (var group in itemsElement.EnumerateArray())
			{
				if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("quality", out var rootQuality))
					return Fail($"items[{index}].quality", "Each quality item must include a quality identity.", out request, out error);
				var rootId = ReadQualityId(rootQuality);
				if (rootId is null) return Fail($"items[{index}].quality.id", "A numeric quality id is required.", out request, out error);
				if (group.TryGetProperty("allowed", out var groupAllowed) && groupAllowed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
					return Fail($"items[{index}].allowed", "allowed must be a boolean.", out request, out error);
				var allowed = ReadBool(group, "allowed", true);
				var leaves = new List<JsonElement>();
				if (group.TryGetProperty("items", out var children))
				{
					if (children.ValueKind != JsonValueKind.Array) return Fail($"items[{index}].items", "Grouped qualities must be an array.", out request, out error);
					leaves.AddRange(children.EnumerateArray());
				}
				if (leaves.Count == 0) leaves.Add(group);
				if (leaves.Count > 1) return Fail($"items[{index}].items", "Equal-quality groups containing distinct qualities cannot be represented by the native profile model.", out request, out error);
				var leaf = leaves[0];
				if (leaf.ValueKind != JsonValueKind.Object)
					return Fail($"items[{index}].items[0]", "A grouped quality entry must be an object.", out request, out error);
				if (leaf.TryGetProperty("allowed", out var leafAllowed) && leafAllowed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
					return Fail($"items[{index}].items[0].allowed", "allowed must be a boolean.", out request, out error);
				var upstreamId = rootId.Value;
				if (leaf.TryGetProperty("quality", out var nested))
				{
					var leafId = ReadQualityId(nested);
					if (leafId is null) return Fail($"items[{index}].items[0].quality.id", "A numeric quality id is required.", out request, out error);
					upstreamId = leafId.Value;
				}
				if (upstreamIds.Contains(upstreamId))
					return Fail($"items[{index}].quality.id", "A quality cannot occur more than once in a profile.", out request, out error);
				if (!CompatQualityMap.TryGetNativeQuality(upstreamId, facade, out var native))
					return Fail($"items[{index}].quality.id", $"Quality id {upstreamId} is unsupported for {facade}.", out request, out error);
				items.Add(new(new QualityModelResource(native.Source?.ToString(), native.Resolution?.ToString(), native.Name), allowed && ReadBool(leaf, "allowed", true)));
				upstreamIds.Add(upstreamId);
				index++;
			}
		}

		if (items.Count == 0) return Fail("items", "At least one quality item is required.", out request, out error);
		var cutoff = -1;
		if (body.TryGetProperty("cutoff", out var cutoffElement))
		{
			if (cutoffElement.ValueKind != JsonValueKind.Number || !cutoffElement.TryGetInt32(out var cutoffId))
				return Fail("cutoff", "Cutoff must be a numeric quality id.", out request, out error);
			cutoff = upstreamIds.IndexOf(cutoffId);
			if (cutoff < 0) return Fail("cutoff", "Cutoff must identify a quality in items.", out request, out error);
		}
		else if (current is not null && current.Items.ElementAtOrDefault(current.Cutoff) is { } currentCutoff)
		{
			var currentCutoffId = CompatQualityMap.ToUpstreamQualityId(currentCutoff.Quality, facade);
			cutoff = currentCutoffId is { } id ? upstreamIds.IndexOf(id) : -1;
			if (cutoff < 0) return Fail("cutoff", "The current cutoff quality is not present in the updated items.", out request, out error);
		}
		else
			return Fail("cutoff", "A cutoff quality is required.", out request, out error);

		var formats = current?.FormatItems.Select(item => new FormatItemResource(item.CustomFormatId, item.Score)).ToList();
		if (body.TryGetProperty("formatItems", out var formatElement))
		{
			formats = [];
			if (formatElement.ValueKind != JsonValueKind.Null)
			{
				if (formatElement.ValueKind != JsonValueKind.Array) return Fail("formatItems", "Format items must be an array or null.", out request, out error);
				foreach (var format in formatElement.EnumerateArray())
				{
					if (!TryGetNestedInt(format, "format", "id", out var formatId) || !TryGetInt(format, "score", out var score))
						return Fail("formatItems", "Each format item requires a format id and score.", out request, out error);
					formats.Add(new(formatId, score));
				}
			}
		}

		var minFormatScore = ReadInt(body, "minFormatScore", current?.MinFormatScore ?? 0);
		var cutoffFormatScore = ReadInt(body, "cutoffFormatScore", current?.CutoffFormatScore ?? 0);
		var minUpgradeFormatScore = ReadInt(body, "minUpgradeFormatScore", current?.MinUpgradeFormatScore ?? 0);
		request = new(name, upgradeAllowed, cutoff, items, formats, minFormatScore, cutoffFormatScore, minUpgradeFormatScore);
		error = default;
		return true;
	}

	private static object Project(QualityProfile profile, string facade, SubmarineDbContext db)
	{
		var formats = db.CustomFormats.AsNoTracking().ToDictionary(format => format.Id, format => format.Name);
		return new
		{
			id = profile.Id,
			name = profile.Name,
			upgradeAllowed = profile.UpgradeAllowed,
			cutoff = profile.Items.ElementAtOrDefault(profile.Cutoff) is { } cutoffItem ? CompatQualityMap.ToUpstreamQualityId(cutoffItem.Quality, facade) : null,
			items = profile.Items.Select(item =>
			{
				var id = CompatQualityMap.ToUpstreamQualityId(item.Quality, facade);
				return new
				{
					quality = new
					{
						id,
						name = id is { } qualityId ? CompatQualityMap.ToUpstreamQualityName(qualityId, facade) : item.Quality.Name,
						source = UpstreamSource(facade, item.Quality.Source),
						modifier = item.Quality.Source == QualitySource.BLURAY_DISK ? "BR-DISK" : null,
						resolution = UpstreamResolution(item.Quality.Resolution)
					},
					items = Array.Empty<object>(),
					allowed = item.Allowed
				};
			}).ToArray(),
			formatItems = profile.FormatItems.Select(item => new { format = new { id = item.CustomFormatId, name = formats.GetValueOrDefault(item.CustomFormatId) }, score = item.Score }).ToArray(),
			minFormatScore = profile.MinFormatScore,
			cutoffFormatScore = profile.CutoffFormatScore,
			minUpgradeFormatScore = profile.MinUpgradeFormatScore
		};
	}

	private static string? UpstreamSource(string facade, QualitySource? source)
		=> (facade, source) switch
		{
			("sonarr", QualitySource.UNKNOWN) => "unknown",
			("sonarr", QualitySource.TV) => "television",
			("sonarr", QualitySource.DVD) => "dvd",
			("sonarr", QualitySource.WEB_DL) => "web",
			("sonarr", QualitySource.WEB_RIP) => "webRip",
			("sonarr", QualitySource.BLURAY) => "bluray",
			("sonarr", QualitySource.BLURAY_REMUX) => "blurayRaw",
			("sonarr", QualitySource.RAW_HD) => "televisionRaw",
			("radarr", QualitySource.UNKNOWN) => "unknown",
			("radarr", QualitySource.TV) => "tv",
			("radarr", QualitySource.DVD) => "dvd",
			("radarr", QualitySource.WEB_DL) => "webdl",
			("radarr", QualitySource.WEB_RIP) => "webrip",
			("radarr", QualitySource.BLURAY or QualitySource.BLURAY_REMUX or QualitySource.BLURAY_DISK) => "bluray",
			("radarr", QualitySource.RAW_HD) => "tv",
			("radarr", QualitySource.CAM) => "cam",
			_ => null
		};

	private static int? UpstreamResolution(QualityResolution? resolution)
		=> resolution switch
		{
			QualityResolution.R480_P => 480,
			QualityResolution.R576_P => 576,
			QualityResolution.R720_P => 720,
			QualityResolution.R1080_P => 1080,
			QualityResolution.R2160_P => 2160,
			_ => null
		};

	private static string? ReadString(JsonElement element, string property, string? fallback)
		=> !element.TryGetProperty(property, out var value) ? fallback : value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	private static bool ReadBool(JsonElement element, string property, bool fallback)
		=> !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.True;
	private static int ReadInt(JsonElement element, string property, int fallback)
		=> element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : fallback;
	private static int? ReadQualityId(JsonElement element)
		=> element.ValueKind == JsonValueKind.Object && TryGetInt(element, "id", out var id) ? id : null;
	private static bool HasId(JsonElement element) => element.TryGetProperty("id", out _);
	private static bool TryGetInt(JsonElement element, string property, out int value)
	{
		value = 0;
		return element.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.Number && propertyValue.TryGetInt32(out value);
	}
	private static bool TryGetNestedInt(JsonElement element, string parent, string property, out int value)
	{
		value = 0;
		return element.TryGetProperty(parent, out var nested) && nested.ValueKind == JsonValueKind.Object && TryGetInt(nested, property, out value);
	}
	private static bool Fail(string property, string message, out QualityProfileRequest request, out (string Property, string Message) error)
	{
		request = null!;
		error = (property, message);
		return false;
	}

	private static async Task<JsonElement?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
	{
		try
		{
			using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
			return document.RootElement.Clone();
		}
		catch (JsonException)
		{
			return null;
		}
	}

}
