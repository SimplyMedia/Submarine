using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Features.Compat.Shared;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Notifications;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Notifications;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;

namespace Submarine.Api.Features.Compat.Prowlarr;

public sealed class ProwlarrModule : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var api = CompatRoutes.CreateRestGroup(endpoints, "prowlarr", "v1");
		api.MapGet("/indexer", IndexersAsync);
		api.MapGet("/indexer/{id:int}", IndexerAsync);
		api.MapGet("/indexer/schema", IndexerSchemaAsync);
		api.MapGet("/indexerstats", IndexerStatsAsync);
		api.MapGet("/search", SearchAsync);
		api.MapGet("/notification", NotificationsAsync);
		api.MapGet("/notification/{id:int}", NotificationAsync);
		api.MapGet("/notification/schema", NotificationSchema);
		api.MapPost("/notification", CreateNotificationAsync);
		api.MapPut("/notification", UpsertNotificationAsync);
		api.MapPut("/notification/{id:int}", UpdateNotificationAsync);
		api.MapDelete("/notification/{id:int}", DeleteNotificationAsync);
		api.MapPost("/notification/test", TestNotificationAsync);
	}

	private static async Task<IResult> IndexersAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var indexers = await db.Indexers.AsNoTracking().Include(x => x.Tags).OrderBy(x => x.Id).ToListAsync(cancellationToken);
		var definitions = await db.IndexerDefinitions.AsNoTracking().ToDictionaryAsync(x => x.DefinitionId, cancellationToken);
		return Results.Json(indexers.Select(x => ToIndexer(x, definitions.GetValueOrDefault(x.DefinitionId ?? ""))).ToArray(), CompatJson.Options);
	}

	private static async Task<IResult> IndexerAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var indexer = await db.Indexers.AsNoTracking().Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (indexer is null) return CompatErrors.Message($"Indexer {id} does not exist", StatusCodes.Status404NotFound);
		var definition = indexer.DefinitionId is null ? null : await db.IndexerDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.DefinitionId == indexer.DefinitionId, cancellationToken);
		return Results.Json(ToIndexer(indexer, definition), CompatJson.Options);
	}

	private static object ToIndexer(Indexer indexer, IndexerDefinition? definition)
	{
		var fields = new List<object>();
		using var document = JsonDocument.Parse(indexer.SettingsJson);
		if (document.RootElement.ValueKind == JsonValueKind.Object)
		{
			foreach (var property in document.RootElement.EnumerateObject())
				fields.Add(new { name = property.Name, value = property.Value.Clone(), type = "textbox", advanced = false, hidden = false, privacy = "normal" });
		}
		var privacy = definition?.Type switch
		{
			IndexerDefinitionType.PRIVATE => "private",
			IndexerDefinitionType.SEMI_PRIVATE => "semiPrivate",
			IndexerDefinitionType.PUBLIC => "public",
			_ => null
		};
		return new
		{
			id = indexer.Id,
			name = indexer.Name,
			protocol = indexer.Protocol.ToString().ToLowerInvariant(),
			enable = indexer.EnableRss || indexer.EnableAutomaticSearch || indexer.EnableInteractiveSearch,
			priority = indexer.Priority,
			implementation = indexer.Implementation switch { IndexerImplementation.TORZNAB => "Torznab", IndexerImplementation.NEWZNAB => "Newznab", _ => "Cardigann" },
			implementationName = indexer.Implementation switch { IndexerImplementation.TORZNAB => "Torznab", IndexerImplementation.NEWZNAB => "Newznab", _ => "Cardigann" },
			configContract = indexer.Implementation switch { IndexerImplementation.TORZNAB => "TorznabSettings", IndexerImplementation.NEWZNAB => "NewznabSettings", _ => "CardigannSettings" },
			fields,
			tags = indexer.Tags.Select(x => x.Id).Order().ToArray(),
			capabilities = new { categories = indexer.Categories.Concat(indexer.AnimeCategories).Distinct().Order().ToArray() },
			privacy,
			added = indexer.CreatedAt
		};
	}

	private static IResult IndexerSchemaAsync(IndexerDefinitionLoader loader)
		=> Results.Json(loader.LoadAll().Select(definition => new { id = definition.Id, name = definition.Name, implementation = "Cardigann", protocol = (definition.Protocol ?? "torrent").ToLowerInvariant(), privacy = definition.Type }).ToArray(), CompatJson.Options);


	private static async Task<IResult> IndexerStatsAsync(SubmarineDbContext db, DateTime? startDate, DateTime? endDate, string? indexerIds, CancellationToken cancellationToken)
	{
		var start = startDate ?? DateTime.UtcNow.AddDays(-30);
		var end = endDate ?? DateTime.UtcNow;
		var ids = string.IsNullOrWhiteSpace(indexerIds) ? null : indexerIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToHashSet();
		var rows = await db.IndexerHistories.AsNoTracking().Where(x => x.Date >= start && x.Date <= end && (ids == null || ids.Contains(x.IndexerId))).ToListAsync(cancellationToken);
		var names = await db.Indexers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
		var stats = rows.GroupBy(x => x.IndexerId).Select(group => new
		{
			indexerId = group.Key,
			indexerName = names.GetValueOrDefault(group.Key, "Unknown"),
			averageResponseTime = group.Where(x => x.ElapsedMs.HasValue).Select(x => (double?)x.ElapsedMs!.Value).Average() ?? 0,
			averageGrabResponseTime = group.Where(x => x.EventType == IndexerHistoryEventType.GRAB && x.ElapsedMs.HasValue).Select(x => (double?)x.ElapsedMs!.Value).Average() ?? 0,
			numberOfQueries = group.Count(x => x.EventType is IndexerHistoryEventType.QUERY or IndexerHistoryEventType.RSS),
			numberOfGrabs = group.Count(x => x.EventType == IndexerHistoryEventType.GRAB),
			numberOfRssQueries = group.Count(x => x.EventType == IndexerHistoryEventType.RSS),
			numberOfAuthQueries = group.Count(x => x.EventType == IndexerHistoryEventType.AUTH),
			numberOfFailedQueries = 0,
			numberOfFailedGrabs = group.Count(x => x.EventType == IndexerHistoryEventType.GRAB && !x.Successful),
			numberOfFailedRssQueries = 0,
			numberOfFailedAuthQueries = 0
		}).ToArray();
		var userAgents = rows.Where(x => x.Source?.StartsWith("newznab:", StringComparison.Ordinal) == true)
			.GroupBy(x => x.Source!["newznab:".Length..])
			.Select(group => new { name = group.Key, numberOfQueries = group.Count(x => x.EventType is IndexerHistoryEventType.QUERY or IndexerHistoryEventType.RSS), numberOfGrabs = group.Count(x => x.EventType == IndexerHistoryEventType.GRAB) })
			.ToArray();
		return Results.Json(new { indexers = stats, userAgents, hosts = Array.Empty<object>() }, CompatJson.Options);
	}

	private static async Task<IResult> SearchAsync(ReleaseSearchService search, string? query, string? type, string? categories, string? indexerIds, int? limit, int? offset, CancellationToken cancellationToken)
	{
		var categoryIds = ParseIds(categories) ?? [];
		var selected = ParseIds(indexerIds);
		SearchRequest request = (type ?? "search").ToLowerInvariant() switch
		{
			"tvsearch" => new TvSearchRequest(Query: query, Categories: categoryIds, Limit: limit, Offset: offset),
			"movie" => new MovieSearchRequest(Query: query, Categories: categoryIds, Limit: limit, Offset: offset),
			"music" => new MusicSearchRequest(Query: query, Categories: categoryIds, Limit: limit, Offset: offset),
			"book" => new BookSearchRequest(Query: query, Categories: categoryIds, Limit: limit, Offset: offset),
			"search" => new BasicSearchRequest(query, categoryIds, limit, offset),
			_ => throw new ArgumentException($"Unsupported search type '{type}'")
		};
		var results = await search.SearchAsync(request, IndexerSearchMode.INTERACTIVE, "compat-prowlarr", cancellationToken, selected);
		return Results.Json(results.Select(x => new
		{
			guid = x.Guid,
			title = x.Title,
			indexerId = x.IndexerId,
			indexer = x.Indexer,
			protocol = x.Protocol.ToString().ToLowerInvariant(),
			size = x.Size,
			publishDate = x.PublishDate,
			categories = x.Categories,
			seeders = x.Seeders,
			leechers = x.Leechers,
			peers = x.Peers,
			grabs = x.Grabs,
			downloadVolumeFactor = x.DownloadVolumeFactor,
			uploadVolumeFactor = x.UploadVolumeFactor,
			infoUrl = x.InfoUrl,
			downloadUrl = x.DownloadUrl,
			magnetUrl = x.MagnetUrl,
			imdbId = x.ImdbId
		}).ToArray(), CompatJson.Options);
	}

	private static int[]? ParseIds(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();

	private static async Task<IResult> NotificationsAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var values = await db.Notifications.AsNoTracking().Where(x => x.Type == NotificationType.NOTIFIARR || x.Type == NotificationType.WEBHOOK)
			.Include(x => x.Tags).OrderBy(x => x.Id).ToListAsync(cancellationToken);
		return Results.Json(values.Select(ToNotification).ToArray(), CompatJson.Options);
	}

	private static async Task<IResult> NotificationAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var value = await db.Notifications.AsNoTracking().Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		return value is null ? CompatErrors.Message($"Notification {id} does not exist", StatusCodes.Status404NotFound) : Results.Json(ToNotification(value), CompatJson.Options);
	}

	private static IResult NotificationSchema()
	{
		object[] result =
		[
			new
			{
				implementation = "Notifiarr",
				implementationName = "Notifiarr",
				configContract = "NotifiarrSettings",
				fields = new[] { new { name = "apiKey", label = "API Key", type = "password", value = "", advanced = false, hidden = false, privacy = "password" } },
				supportsOnGrab = true,
				supportsOnHealthIssue = true,
				supportsOnHealthRestored = true,
				supportsOnApplicationUpdate = true
			},
			new
			{
				implementation = "Webhook",
				implementationName = "Webhook",
				configContract = "WebhookSettings",
				fields = NotificationSettingsJson.Describe(NotificationType.WEBHOOK)
					.Select(field => (object)new { name = field.Name, label = field.Label, type = field.Type, value = field.Default, advanced = false, hidden = false, privacy = field.Type == "password" ? "password" : "normal" })
					.ToArray(),
				supportsOnGrab = true,
				supportsOnHealthIssue = true,
				supportsOnHealthRestored = true,
				supportsOnApplicationUpdate = true
			}
		];
		return Results.Json(result, CompatJson.Options);
	}

	private static object ToNotification(Notification value)
	{
		var type = value.Type == NotificationType.NOTIFIARR ? "Notifiarr" : "Webhook";
		using var document = JsonDocument.Parse(value.SettingsJson);
		var fields = document.RootElement.EnumerateObject().Select(p => new { name = p.Name, value = (object?)p.Value.Clone() }).ToArray();
		return new { id = value.Id, name = value.Name, implementation = type, implementationName = type, configContract = type == "Notifiarr" ? "NotifiarrSettings" : "WebhookSettings", fields, tags = value.Tags.Select(x => x.Id).ToArray(), onGrab = value.OnGrab, onHealthIssue = value.OnHealthIssue, onHealthRestored = value.OnHealthRestored, onApplicationUpdate = value.OnApplicationUpdate, supportsOnGrab = true, supportsOnHealthIssue = true, supportsOnHealthRestored = true, supportsOnApplicationUpdate = true };
	}

	private static async Task<IResult> CreateNotificationAsync(ProwlarrNotificationRequest request, SubmarineDbContext db, NotificationConfigurationService service, CancellationToken cancellationToken)
		=> await SaveNotificationAsync(request.Id, request, db, service, cancellationToken);

	private static async Task<IResult> UpsertNotificationAsync(ProwlarrNotificationRequest request, SubmarineDbContext db, NotificationConfigurationService service, CancellationToken cancellationToken)
		=> await SaveNotificationAsync(request.Id, request, db, service, cancellationToken);

	private static async Task<IResult> UpdateNotificationAsync(int id, ProwlarrNotificationRequest request, SubmarineDbContext db, NotificationConfigurationService service, CancellationToken cancellationToken)
		=> await SaveNotificationAsync(id, request with { Id = id }, db, service, cancellationToken);

	private static async Task<IResult> SaveNotificationAsync(
		int? id,
		ProwlarrNotificationRequest request,
		SubmarineDbContext db,
		NotificationConfigurationService service,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name)) return CompatErrors.Message("Notification name is required", StatusCodes.Status400BadRequest);
		var type = request.Implementation.Equals("Notifiarr", StringComparison.OrdinalIgnoreCase) ? NotificationType.NOTIFIARR
			: request.Implementation.Equals("Webhook", StringComparison.OrdinalIgnoreCase) ? NotificationType.WEBHOOK
			: (NotificationType?)null;
		if (type is null) return CompatErrors.Message($"Unsupported notification implementation '{request.Implementation}'", StatusCodes.Status400BadRequest);
		var settings = request.Fields.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
		var settingsJson = JsonSerializer.Serialize(settings, CompatJson.Options);
		if (!NotificationSettingsJson.Validate(type.Value, settingsJson).IsValid) return CompatErrors.Message("Notification fields are invalid", StatusCodes.Status400BadRequest);
		var tagIds = request.Tags?.Distinct().ToArray() ?? [];
		var tags = await db.Tags.Where(x => tagIds.Contains(x.Id)).Select(x => x.Label).ToListAsync(cancellationToken);
		if (tags.Count != tagIds.Length) return CompatErrors.Message("One or more notification tags do not exist", StatusCodes.Status400BadRequest);
		var saved = await service.SaveAsync(id, new NotificationConfiguration(
			request.Name, type.Value, true, settingsJson, request.OnGrab, request.OnImport, request.OnUpgrade,
			request.OnRename, request.OnDelete, request.OnHealthIssue, request.OnHealthRestored, request.OnApplicationUpdate,
			false, request.IncludeHealthWarnings, tags), cancellationToken);
		return Results.Json(ToNotification(saved), CompatJson.Options, statusCode: id is null ? StatusCodes.Status201Created : StatusCodes.Status200OK);
	}

	private static async Task<IResult> DeleteNotificationAsync(int id, NotificationConfigurationService service, CancellationToken cancellationToken)
	{
		await service.DeleteAsync(id, cancellationToken);
		return Results.NoContent();
	}

	private static async Task<IResult> TestNotificationAsync(ProwlarrNotificationRequest request, INotificationSenderFactory factory, CancellationToken cancellationToken)
	{
		var type = request.Implementation.Equals("Notifiarr", StringComparison.OrdinalIgnoreCase) ? NotificationType.NOTIFIARR : NotificationType.WEBHOOK;
		var settings = JsonSerializer.Serialize(request.Fields.ToDictionary(x => x.Name, x => x.Value), CompatJson.Options);
		await factory.Resolve(type).TestAsync(settings, cancellationToken);
		return Results.NoContent();
	}
}

public sealed record ProwlarrNotificationField(string Name, JsonElement Value);
public sealed record ProwlarrNotificationRequest(int? Id, string Name, string Implementation, string? ConfigContract, IReadOnlyList<ProwlarrNotificationField> Fields, IReadOnlyList<int>? Tags = null, bool OnGrab = false, bool OnImport = false, bool OnUpgrade = false, bool OnRename = false, bool OnDelete = false, bool OnHealthIssue = false, bool OnHealthRestored = false, bool OnApplicationUpdate = false, bool IncludeHealthWarnings = false);
