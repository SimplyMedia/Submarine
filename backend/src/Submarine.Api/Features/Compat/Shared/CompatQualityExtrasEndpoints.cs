using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.CustomFormats;
using Submarine.Api.Features.QualityDefinitions;
using Submarine.Api.Modules;
using Submarine.Core.CustomFormats;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Sonarr and Radarr quality-definition and custom-format routes.</summary>
public sealed class CompatQualityExtrasEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr");
		MapFacade(endpoints, "radarr");
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, "v3");
		group.MapGet("/qualitydefinition", (SubmarineDbContext db, CancellationToken ct) => GetQualityDefinitionsAsync(db, facade, ct));
		group.MapPut("/qualitydefinition/update", (HttpRequest request, QualityDefinitionService service, SubmarineDbContext db, CancellationToken ct) => UpdateQualityDefinitionsAsync(request, facade, service, db, ct));
		group.MapGet("/customformat", (SubmarineDbContext db, CancellationToken ct) => GetCustomFormatsAsync(facade, db, ct));
		group.MapGet("/customformat/schema", () => Results.Json(BuildCustomFormatSchema(facade), CompatJson.Options));
		group.MapGet("/customformat/{id:int}", (int id, SubmarineDbContext db, CancellationToken ct) => GetCustomFormatAsync(facade, id, db, ct));
		group.MapPost("/customformat", (HttpRequest request, CustomFormatService service, CancellationToken ct) => CreateCustomFormatAsync(facade, request, service, ct));
		group.MapPut("/customformat", (HttpRequest request, CustomFormatService service, SubmarineDbContext db, CancellationToken ct) => UpdateCustomFormatsAsync(facade, request, service, db, ct));
		group.MapPut("/customformat/{id:int}", (int id, HttpRequest request, CustomFormatService service, SubmarineDbContext db, CancellationToken ct) => UpdateCustomFormatAsync(facade, id, request, service, db, ct));
		group.MapDelete("/customformat/{id:int}", DeleteCustomFormatAsync);
	}

	private static async Task<IResult> GetQualityDefinitionsAsync(SubmarineDbContext db, string facade, CancellationToken ct)
	{
		var entries = await db.QualityDefinitions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
		var result = new List<object>(entries.Count);
		foreach (var definition in entries)
		{
			var model = new QualityResolutionModel(definition.Source, definition.Resolution);
			var id = CompatQualityMap.ToUpstreamQualityId(model, facade);
			if (id is null) continue;
			result.Add(new { id, name = definition.Title, source = SourceValue(definition.Source), resolution = ResolutionValue(definition.Resolution), modifier = "", preferredSize = definition.PreferredSizeMbPerMinute, minSize = definition.MinSizeMbPerMinute, maxSize = definition.MaxSizeMbPerMinute, title = definition.Title, weight = id.Value });
		}
		return Results.Json(result, CompatJson.Options);
	}

	private static async Task<IResult> UpdateQualityDefinitionsAsync(HttpRequest request, string facade, QualityDefinitionService service, SubmarineDbContext db, CancellationToken ct)
	{
		var body = await ReadBodyAsync(request, ct);
		if (body is not { ValueKind: JsonValueKind.Array } entries) return CompatErrors.Validation("", "A quality definition array is required.");
		var definitions = await db.QualityDefinitions.ToListAsync(ct);
		var updates = new List<QualityDefinitionUpdate>();
		foreach (var entry in entries.EnumerateArray())
		{
			if (entry.ValueKind != JsonValueKind.Object || !TryGetInt(entry, "id", out var id) || !CompatQualityMap.TryGetNativeQuality(id, facade, out var mapped))
				return CompatErrors.Validation("id", "Each quality definition requires a supported upstream quality id.");
			var definition = definitions.FirstOrDefault(item => item.Source == mapped.Source && item.Resolution == mapped.Resolution);
			if (definition is null) return CompatErrors.Validation("id", $"Quality id {id} does not identify a native quality definition.");
			if (!TryNullableDouble(entry, "minSize", definition.MinSizeMbPerMinute, out var min) || !TryNullableDouble(entry, "maxSize", definition.MaxSizeMbPerMinute, out var max) || !TryNullableDouble(entry, "preferredSize", definition.PreferredSizeMbPerMinute, out var preferred))
				return CompatErrors.Validation("", "Quality sizes must be numbers or null.");
			if (min is { } lower && max is { } upper && lower > upper)
				return CompatErrors.Validation("minSize", "Minimum size must not exceed maximum size.");
			updates.Add(new QualityDefinitionUpdate(definition.Id, min, max, preferred));
		}
		try
		{
			await service.UpdateAsync(updates, ct);
			return Results.Ok();
		}
		catch (KeyNotFoundException exception) { return CompatErrors.Message(exception.Message, StatusCodes.Status404NotFound); }
		catch (FluentValidation.ValidationException exception) { return CompatErrors.Validation("", exception.Message); }
	}

	private static async Task<IResult> GetCustomFormatsAsync(string facade, SubmarineDbContext db, CancellationToken ct)
	{
		var formats = await db.CustomFormats.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
		return Results.Json(formats.Select(format => ProjectFormat(format, facade)).ToArray(), CompatJson.Options);
	}

	private static async Task<IResult> GetCustomFormatAsync(string facade, int id, SubmarineDbContext db, CancellationToken ct)
	{
		var format = await db.CustomFormats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
		return format is null ? CompatErrors.Message("Custom format not found.", StatusCodes.Status404NotFound) : Results.Json(ProjectFormat(format, facade), CompatJson.Options);
	}

	private static async Task<IResult> CreateCustomFormatAsync(string facade, HttpRequest request, CustomFormatService service, CancellationToken ct)
	{
		var body = await ReadBodyAsync(request, ct);
		if (body is not { ValueKind: JsonValueKind.Object } objectBody) return CompatErrors.Validation("", "A custom format object is required.");
		if (!TryFormatRequest(objectBody, null, facade, out var formatRequest, out var error)) return CompatErrors.Validation(error.Property, error.Message);
		try
		{
			var format = await service.CreateAsync(formatRequest, ct);
			request.HttpContext.Response.Headers.Location = $"/compat/{facade}/api/v3/customformat/{format.Id}";
			return Results.Json(ProjectFormat(format, facade), CompatJson.Options, statusCode: StatusCodes.Status201Created);
		}
		catch (Submarine.Core.Common.ConflictException exception) { return CompatErrors.Message(exception.Message, StatusCodes.Status409Conflict); }
		catch (FluentValidation.ValidationException exception) { return CompatErrors.Validation("", exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message); }
	}

	private static async Task<IResult> UpdateCustomFormatAsync(string facade, int id, HttpRequest request, CustomFormatService service, SubmarineDbContext db, CancellationToken ct)
	{
		if (await ReadBodyAsync(request, ct) is not { ValueKind: JsonValueKind.Object } body) return CompatErrors.Validation("", "A custom format object is required.");
		if (body.TryGetProperty("id", out var idElement) && (!idElement.TryGetInt32(out var bodyId) || bodyId != id)) return CompatErrors.Validation("id", "The route id must match the custom format id.");
		var existing = await db.CustomFormats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
		if (existing is null) return CompatErrors.Message("Custom format not found.", StatusCodes.Status404NotFound);
		if (!TryFormatRequest(body, existing, facade, out var formatRequest, out var error)) return CompatErrors.Validation(error.Property, error.Message);
		try
		{
			var updated = await service.UpdateAsync(id, formatRequest, ct);
			return updated is null ? CompatErrors.Message("Custom format not found.", StatusCodes.Status404NotFound) : Results.Json(ProjectFormat(updated, facade), CompatJson.Options);
		}
		catch (Submarine.Core.Common.ConflictException exception) { return CompatErrors.Message(exception.Message, StatusCodes.Status409Conflict); }
		catch (FluentValidation.ValidationException exception) { return CompatErrors.Validation("", exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message); }
	}

	private static async Task<IResult> UpdateCustomFormatsAsync(string facade, HttpRequest request, CustomFormatService service, SubmarineDbContext db, CancellationToken ct)
	{
		if (await ReadBodyAsync(request, ct) is not { ValueKind: JsonValueKind.Array } entries) return CompatErrors.Validation("", "A custom format array is required.");
		var items = new List<(int Id, CustomFormatRequest Request)>();
		foreach (var entry in entries.EnumerateArray())
		{
			if (entry.ValueKind != JsonValueKind.Object || !TryGetInt(entry, "id", out var id)) return CompatErrors.Validation("id", "Each custom format requires an id.");
			var existing = await db.CustomFormats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
			if (existing is null) return CompatErrors.Message($"Custom format {id} not found.", StatusCodes.Status404NotFound);
			if (!TryFormatRequest(entry, existing, facade, out var formatRequest, out var error)) return CompatErrors.Validation(error.Property, error.Message);
			items.Add((id, formatRequest));
		}
		await using var transaction = await db.Database.BeginTransactionAsync(ct);
		try
		{
			var result = new List<object>(items.Count);
			foreach (var (id, formatRequest) in items)
			{
				var format = await service.UpdateAsync(id, formatRequest, ct) ?? throw new KeyNotFoundException();
				result.Add(ProjectFormat(format, facade));
			}
			await transaction.CommitAsync(ct);
			return Results.Json(result, CompatJson.Options);
		}
		catch (Submarine.Core.Common.ConflictException exception)
		{
			await transaction.RollbackAsync(ct);
			return CompatErrors.Message(exception.Message, StatusCodes.Status409Conflict);
		}
		catch (FluentValidation.ValidationException exception)
		{
			await transaction.RollbackAsync(ct);
			return CompatErrors.Validation("", exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message);
		}
	}

	private static async Task<IResult> DeleteCustomFormatAsync(int id, CustomFormatService service, CancellationToken ct)
		=> await service.DeleteAsync(id, ct) ? Results.Ok() : CompatErrors.Message("Custom format not found.", StatusCodes.Status404NotFound);

	private static bool TryFormatRequest(JsonElement body, CustomFormat? current, string facade, out CustomFormatRequest request, out (string Property, string Message) error)
	{
		var name = ReadString(body, "name", current?.Name);
		if (string.IsNullOrWhiteSpace(name)) return FormatFailure("name", "A custom format name is required.", out request, out error);
		var rename = current?.IncludeCustomFormatWhenRenaming ?? false;
		if (body.TryGetProperty("includeCustomFormatWhenRenaming", out _) && !TryReadBool(body, "includeCustomFormatWhenRenaming", out rename))
			return FormatFailure("includeCustomFormatWhenRenaming", "The value must be a boolean.", out request, out error);
		var specs = current?.Specifications.Select(ToResource).ToList() ?? [];
		if (body.TryGetProperty("specifications", out var specArray))
		{
			if (specArray.ValueKind != JsonValueKind.Array) return FormatFailure("specifications", "Specifications must be an array.", out request, out error);
			specs = [];
			foreach (var spec in specArray.EnumerateArray())
			{
				var implementation = spec.ValueKind == JsonValueKind.Object ? ReadString(spec, "implementation", null) : null;
				if (implementation is null || !ImplementationTypes.TryGetValue(implementation, out var type))
					return FormatFailure("specifications", "Every specification requires a supported implementation.", out request, out error);
				if (!TryReadBool(spec, "negate", out var negate) || !TryReadBool(spec, "required", out var required)) return FormatFailure("specifications", "Specification negate and required values must be booleans.", out request, out error);
				if (!spec.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array) return FormatFailure("specifications", "Specification fields must be an array.", out request, out error);
				var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
				foreach (var field in fields.EnumerateArray())
				{
					if (field.ValueKind != JsonValueKind.Object || ReadString(field, "name", null) is not { } fieldName || !field.TryGetProperty("value", out var fieldValue)) return FormatFailure("specifications", "Each field requires a name and value.", out request, out error);
					values[fieldName] = fieldValue.Clone();
				}
				var value = MapFieldValue(type, values, facade, out var mapError);
				if (mapError is not null) return FormatFailure("specifications", mapError, out request, out error);
				specs.Add(new SpecificationResource(ReadString(spec, "name", implementation)!, type, negate, required, value));
			}
		}
		request = new CustomFormatRequest(name, rename, specs);
		error = default;
		return true;
	}

	private static JsonElement? MapFieldValue(CustomFormatSpecificationType type, Dictionary<string, JsonElement> fields, string facade, out string? error)
	{
		error = null;
		if (type is CustomFormatSpecificationType.SIZE or CustomFormatSpecificationType.YEAR)
		{
			var range = new Dictionary<string, object?>();
			foreach (var bound in new[] { "min", "max" })
				if (fields.TryGetValue(bound, out var number))
				{
					if (number.ValueKind != JsonValueKind.Number || !number.TryGetDouble(out var value)) { error = $"{bound} must be numeric."; return null; }
					range[bound] = value;
				}
			return JsonSerializer.SerializeToElement(range);
		}
		if (!fields.TryGetValue("value", out var input)) return null;
		if (input.ValueKind == JsonValueKind.Number)
		{
			if (!TryMapNumericEnum(type, input, facade, out var enumName)) { error = $"The numeric {type} value is unsupported."; return null; }
			return JsonSerializer.SerializeToElement(enumName);
		}
		if (input.ValueKind is not (JsonValueKind.String or JsonValueKind.True or JsonValueKind.False)) { error = $"The {type} value has an unsupported type."; return null; }
		return input.Clone();
	}

	private static bool TryMapNumericEnum(CustomFormatSpecificationType type, JsonElement input, string facade, out string name)
	{
		name = "";
		if (!input.TryGetInt32(out var id)) return false;
		if (type == CustomFormatSpecificationType.LANGUAGE && CompatLanguageMap.TryGetNativeLanguage(id, facade, out var language)) { name = language.ToString(); return true; }
		if (type == CustomFormatSpecificationType.QUALITY_SOURCE)
		{
			name = facade == "sonarr"
				? id switch { 0 => "UNKNOWN", 1 => "TV", 2 => "RAW_HD", 3 => "WEB_DL", 4 => "WEB_RIP", 5 => "DVD", 6 => "BLURAY", 7 => "BLURAY_REMUX", _ => "" }
				: id switch { 0 => "UNKNOWN", 1 => "CAM", 4 => "DVD", 5 => "TV", 6 => "WEB_DL", 7 => "WEB_RIP", 8 => "BLURAY", 9 => "RAW_HD", 10 => "BLURAY_REMUX", _ => "" };
			return name.Length > 0;
		}
		if (type == CustomFormatSpecificationType.QUALITY_MODIFIER)
		{
			name = id switch { 0 => "NONE", 3 => "RAW_HD", 4 => "BLURAY_DISK", 5 => "BLURAY_REMUX", _ => "" };
			return name.Length > 0;
		}
		if (type == CustomFormatSpecificationType.RESOLUTION)
		{
			name = id switch { 360 => "R360_P", 480 => "R480_P", 540 => "R540_P", 576 => "R576_P", 720 => "R720_P", 1080 => "R1080_P", 2160 => "R2160_P", _ => "" };
			return name.Length > 0;
		}
		if (type == CustomFormatSpecificationType.PROTOCOL)
		{
			name = id switch { 0 => "BITTORRENT", 1 => "USENET", _ => "" };
			return name.Length > 0;
		}
		return false;
	}

	private static object ProjectFormat(CustomFormat format, string facade) => new
	{
		id = format.Id,
		name = format.Name,
		includeCustomFormatWhenRenaming = format.IncludeCustomFormatWhenRenaming,
		specifications = format.Specifications.Select(spec => new
		{
			name = spec.Name,
			implementation = ImplementationNames[spec.Type],
			negate = spec.Negate,
			required = spec.Required,
			fields = ProjectFields(spec, facade)
		}).ToArray()
	};

	private static object[] ProjectFields(CustomFormatSpecification spec, string facade)
	{
		var fields = new List<object>();
		if (spec.Value is not { } value) return [];
		if (value.ValueKind == JsonValueKind.Object && spec.Type is CustomFormatSpecificationType.SIZE or CustomFormatSpecificationType.YEAR)
		{
			foreach (var bound in new[] { "min", "max" }) if (value.TryGetProperty(bound, out var field)) fields.Add(new { name = bound, value = field.Clone() });
		}
		else if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var inner)) fields.Add(new { name = "value", value = ProjectEnum(spec.Type, inner, facade) });
		else fields.Add(new { name = "value", value = ProjectEnum(spec.Type, value, facade) });
		return fields.ToArray();
	}

	private static object ProjectEnum(CustomFormatSpecificationType type, JsonElement value, string facade)
	{
		if (value.ValueKind != JsonValueKind.String) return value.Clone();
		var name = value.GetString();
		if (type == CustomFormatSpecificationType.LANGUAGE && Enum.TryParse<Language>(name, out var language)) return CompatLanguageMap.ToUpstreamId(language, facade);
		if (type == CustomFormatSpecificationType.QUALITY_SOURCE) return UpstreamSourceId(name!, facade) is { } sourceId ? sourceId : name!;
		if (type == CustomFormatSpecificationType.QUALITY_MODIFIER) return name switch { "NONE" => 0, "RAW_HD" => 3, "BLURAY_DISK" => 4, "BLURAY_REMUX" => 5, _ => name! };
		if (type == CustomFormatSpecificationType.RESOLUTION) return name switch { "R360_P" => 360, "R480_P" => 480, "R540_P" => 540, "R576_P" => 576, "R720_P" => 720, "R1080_P" => 1080, "R2160_P" => 2160, _ => name! };
		if (type == CustomFormatSpecificationType.PROTOCOL) return name switch { "BITTORRENT" => 0, "USENET" => 1, _ => name! };
		return value.Clone();
	}

	private static int? UpstreamSourceId(string name, string facade) => facade == "sonarr"
		? name switch { "UNKNOWN" => 0, "TV" => 1, "RAW_HD" => 2, "WEB_DL" => 3, "WEB_RIP" => 4, "DVD" => 5, "BLURAY" => 6, "BLURAY_REMUX" => 7, _ => null }
		: name switch { "UNKNOWN" => 0, "CAM" => 1, "DVD" => 4, "TV" => 5, "WEB_DL" => 6, "WEB_RIP" => 7, "BLURAY" => 8, "RAW_HD" => 9, "BLURAY_REMUX" => 10, _ => null };


	private static SpecificationResource ToResource(CustomFormatSpecification spec) => new(spec.Name, spec.Type, spec.Negate, spec.Required, spec.Value);
	private static bool FormatFailure(string property, string message, out CustomFormatRequest request, out (string Property, string Message) error) { request = null!; error = (property, message); return false; }
	private static async Task<JsonElement?> ReadBodyAsync(HttpRequest request, CancellationToken ct) { try { using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct); return document.RootElement.Clone(); } catch (JsonException) { return null; } }
	private static string? ReadString(JsonElement body, string name, string? fallback) => !body.TryGetProperty(name, out var value) ? fallback : value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	
	private static bool TryReadBool(JsonElement body, string name, out bool value)
	{
		value = false;
		if (!body.TryGetProperty(name, out var item) || item.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
		value = item.GetBoolean();
		return true;
	}
	private static bool TryGetInt(JsonElement body, string name, out int value)
	{
		value = 0;
		return body.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out value);
	}
	private static bool TryNullableDouble(JsonElement body, string name, double? fallback, out double? value) { value = fallback; if (!body.TryGetProperty(name, out var item)) return true; if (item.ValueKind == JsonValueKind.Null) { value = null; return true; } if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number)) return false; value = number; return true; }
	private static int? SourceValue(QualitySource? source) => source switch { QualitySource.UNKNOWN => 0, QualitySource.CAM => 1, QualitySource.TV => 5, QualitySource.WEB_DL => 6, QualitySource.WEB_RIP => 7, QualitySource.BLURAY => 8, QualitySource.RAW_HD => 9, QualitySource.BLURAY_REMUX => 10, QualitySource.DVD => 4, _ => null };
	private static int? ResolutionValue(QualityResolution? resolution) => resolution switch { QualityResolution.R360_P => 360, QualityResolution.R480_P => 480, QualityResolution.R540_P => 540, QualityResolution.R576_P => 576, QualityResolution.R720_P => 720, QualityResolution.R1080_P => 1080, QualityResolution.R2160_P => 2160, _ => null };

	private static readonly IReadOnlyDictionary<string, CustomFormatSpecificationType> ImplementationTypes = new Dictionary<string, CustomFormatSpecificationType>(StringComparer.Ordinal)
	{
		["ReleaseTitleSpecification"] = CustomFormatSpecificationType.RELEASE_TITLE, ["ReleaseGroupSpecification"] = CustomFormatSpecificationType.RELEASE_GROUP,
		["LanguageSpecification"] = CustomFormatSpecificationType.LANGUAGE, ["SourceSpecification"] = CustomFormatSpecificationType.QUALITY_SOURCE,
		["ResolutionSpecification"] = CustomFormatSpecificationType.RESOLUTION, ["EditionSpecification"] = CustomFormatSpecificationType.EDITION,
		["IndexerFlagSpecification"] = CustomFormatSpecificationType.INDEXER_FLAG, ["SizeSpecification"] = CustomFormatSpecificationType.SIZE,
		["YearSpecification"] = CustomFormatSpecificationType.YEAR, ["ProtocolSpecification"] = CustomFormatSpecificationType.PROTOCOL,
		["ReleaseFlagSpecification"] = CustomFormatSpecificationType.RELEASE_FLAG, ["HardcodedSubsSpecification"] = CustomFormatSpecificationType.HARDCODED_SUBS,
		["StreamingProviderSpecification"] = CustomFormatSpecificationType.STREAMING_PROVIDER, ["ReleaseTypeSpecification"] = CustomFormatSpecificationType.RELEASE_TYPE,
		["QualityModifierSpecification"] = CustomFormatSpecificationType.QUALITY_MODIFIER
	};
	private static readonly IReadOnlyDictionary<CustomFormatSpecificationType, string> ImplementationNames = ImplementationTypes.ToDictionary(x => x.Value, x => x.Key);
	private static object[] BuildCustomFormatSchema(string facade)
		=> ImplementationTypes.Select(item => (object)new
		{
			implementation = item.Key,
			name = item.Key.Replace("Specification", "", StringComparison.Ordinal),
			fields = SchemaFields(item.Value, facade),
			negate = true,
			required = true,
			presets = Array.Empty<object>()
		}).ToArray();

	private static object[] SchemaFields(CustomFormatSpecificationType type, string facade)
	{
		if (type is CustomFormatSpecificationType.SIZE or CustomFormatSpecificationType.YEAR)
			return [new { name = "min", label = "Minimum", type = "number", advanced = false, value = (object?)null }, new { name = "max", label = "Maximum", type = "number", advanced = false, value = (object?)null }];
		var fieldType = type is CustomFormatSpecificationType.LANGUAGE or CustomFormatSpecificationType.QUALITY_SOURCE or CustomFormatSpecificationType.QUALITY_MODIFIER or CustomFormatSpecificationType.RESOLUTION or CustomFormatSpecificationType.PROTOCOL ? "select" : type == CustomFormatSpecificationType.HARDCODED_SUBS ? "checkbox" : "textbox";
		var selectOptions = SelectOptions(type, facade);
		return [new { name = "value", label = "Value", type = fieldType, advanced = false, value = (object?)null, selectOptions }];
	}

	private static object[] SelectOptions(CustomFormatSpecificationType type, string facade)
	{
		if (type == CustomFormatSpecificationType.LANGUAGE)
			return CompatLanguageMap.Catalog(facade).Select(language => (object)new { value = language.Id, name = language.Name }).ToArray();
		if (type == CustomFormatSpecificationType.QUALITY_SOURCE)
			return facade == "sonarr"
				? new object[] { new { value = 0, name = "Unknown" }, new { value = 1, name = "TV" }, new { value = 2, name = "RawHD" }, new { value = 3, name = "WebDL" }, new { value = 4, name = "WebRip" }, new { value = 5, name = "DVD" }, new { value = 6, name = "Bluray" }, new { value = 7, name = "BlurayRemux" } }
				: new object[] { new { value = 0, name = "Unknown" }, new { value = 1, name = "Cam" }, new { value = 4, name = "DVD" }, new { value = 5, name = "TV" }, new { value = 6, name = "WebDL" }, new { value = 7, name = "WebRip" }, new { value = 8, name = "Bluray" }, new { value = 9, name = "RawHD" }, new { value = 10, name = "BlurayRemux" } };
		if (type == CustomFormatSpecificationType.QUALITY_MODIFIER)
			return new object[] { new { value = 0, name = "None" }, new { value = 3, name = "RawHD" }, new { value = 4, name = "BlurayDisc" }, new { value = 5, name = "Remux" } };
		if (type == CustomFormatSpecificationType.RESOLUTION)
			return new object[] { new { value = 360, name = "360p" }, new { value = 480, name = "480p" }, new { value = 540, name = "540p" }, new { value = 576, name = "576p" }, new { value = 720, name = "720p" }, new { value = 1080, name = "1080p" }, new { value = 2160, name = "2160p" } };
		if (type == CustomFormatSpecificationType.PROTOCOL)
			return new object[] { new { value = 0, name = "Torrent" }, new { value = 1, name = "Usenet" } };
		return [];
	}
	
}
