using System.Reflection;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Indexers;

/// <summary>
///     Indexer CRUD, connection testing, settings schema and request history.
/// </summary>
public sealed class IndexersModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/indexers");
		group.MapGet("/", ListAsync);
		group.MapGet("/schema", SchemaAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapGet("/{id:int}/capabilities", CapabilitiesAsync);
		group.MapGet("/{id:int}/history", HistoryAsync);
		group.MapPost("/", CreateAsync);
		group.MapPost("/test", TestUnsavedAsync);
		group.MapPost("/test-all", TestAllAsync);
		group.MapPost("/{id:int}/test", TestAsync);
		group.MapPut("/bulk", BulkUpdateAsync);
		group.MapDelete("/bulk", BulkDeleteAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);

		endpoints.MapGet("/api/v1/indexer-categories", CategoriesAsync);
	}

	private static async Task<Ok<PagedResult<IndexerDto>>> ListAsync(
		SubmarineDbContext db,
		IndexerCapabilityCache capabilityCache,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var indexers = await db.Indexers.AsNoTracking().Include(indexer => indexer.Tags).OrderBy(indexer => indexer.Priority).ToListAsync(cancellationToken);
		var statuses = await db.IndexerStatuses.AsNoTracking().ToDictionaryAsync(status => status.IndexerId, cancellationToken);

		return TypedResults.Ok(await PagedResult<IndexerDto>.CreateAsync(
			indexers.Select(indexer => ToDto(indexer, statuses.GetValueOrDefault(indexer.Id), capabilityCache)).AsQueryable(),
			query,
			cancellationToken));
	}

	private static Ok<IndexerSchemaResponse> SchemaAsync(IndexerDefinitionLoader loader)
		=> TypedResults.Ok(new IndexerSchemaResponse(BuildImplementationSchema(), loader.LoadAll().Select(definition => definition.ToInfo()).ToList()));

	private static async Task<Results<Ok<IndexerDto>, NotFound>> GetAsync(
		int id,
		SubmarineDbContext db,
		IndexerCapabilityCache capabilityCache,
		CancellationToken cancellationToken)
	{
		var indexer = await db.Indexers.AsNoTracking().Include(entity => entity.Tags).FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (indexer is null)
		{
			return TypedResults.NotFound();
		}

		var status = await db.IndexerStatuses.AsNoTracking().FirstOrDefaultAsync(entity => entity.IndexerId == id, cancellationToken);
		return TypedResults.Ok(ToDto(indexer, status, capabilityCache));
	}

	private static async Task<Results<Ok<IndexerCapabilitiesSummary>, NotFound>> CapabilitiesAsync(
		int id,
		SubmarineDbContext db,
		IIndexerProvider indexerProvider,
		CancellationToken cancellationToken)
	{
		var indexer = await db.Indexers.AsNoTracking().Include(entity => entity.Tags).FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (indexer is null)
		{
			return TypedResults.NotFound();
		}

		var capabilities = await indexerProvider.GetCapabilitiesAsync(indexer, cancellationToken);
		return TypedResults.Ok(ToSummary(capabilities));
	}

	private static async Task<Results<Ok<PagedResult<IndexerHistoryDto>>, NotFound>> HistoryAsync(
		int id,
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		[FromQuery] IndexerHistoryEventType? eventType,
		[FromQuery] bool? successful,
		CancellationToken cancellationToken)
	{
		if (!await db.Indexers.AnyAsync(entity => entity.Id == id, cancellationToken))
		{
			return TypedResults.NotFound();
		}

		var entries = db.IndexerHistories.AsNoTracking().Where(entry => entry.IndexerId == id);
		if (eventType is not null)
		{
			entries = entries.Where(entry => entry.EventType == eventType);
		}

		if (successful is not null)
		{
			entries = entries.Where(entry => entry.Successful == successful);
		}

		return TypedResults.Ok(await PagedResult<IndexerHistoryDto>.CreateAsync(entries.Select(HistoryDtoExpression), query, cancellationToken));
	}

	private static async Task<Results<Created<IndexerDto>, ValidationProblem>> CreateAsync(
		SubmarineDbContext db,
		IIndexerFactory factory,
		IndexerDefinitionLoader definitionLoader,
		IValidator<IndexerRequest> validator,
		IndexerCapabilityCache capabilityCache,
		[FromBody] IndexerRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		ValidateSettingsOrThrow(factory, definitionLoader, request.Implementation, request.DefinitionId, request.Settings);

		var indexer = new Indexer();
		await ApplyAsync(indexer, request, db, cancellationToken);

		db.Indexers.Add(indexer);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/indexers/{indexer.Id}", ToDto(indexer, null, capabilityCache));
	}

	private static async Task<Results<Ok<IndexerDto>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IIndexerFactory factory,
		IndexerDefinitionLoader definitionLoader,
		IValidator<IndexerRequest> validator,
		IndexerCapabilityCache capabilityCache,
		IIndexerProvider indexerProvider,
		[FromBody] IndexerRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		ValidateSettingsOrThrow(factory, definitionLoader, request.Implementation, request.DefinitionId, request.Settings);

		var indexer = await db.Indexers.Include(entity => entity.Tags).FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (indexer is null)
		{
			return TypedResults.NotFound();
		}

		await ApplyAsync(indexer, request, db, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		indexerProvider.InvalidateCapabilities(id);

		var status = await db.IndexerStatuses.AsNoTracking().FirstOrDefaultAsync(entity => entity.IndexerId == id, cancellationToken);
		return TypedResults.Ok(ToDto(indexer, status, capabilityCache));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(
		int id,
		SubmarineDbContext db,
		IIndexerProvider indexerProvider,
		CancellationToken cancellationToken)
	{
		var indexer = await db.Indexers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (indexer is null)
		{
			return TypedResults.NotFound();
		}

		db.Indexers.Remove(indexer);
		await db.SaveChangesAsync(cancellationToken);
		indexerProvider.InvalidateCapabilities(id);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<IndexerTestResult>> TestUnsavedAsync(
		IIndexerFactory factory,
		IndexerDefinitionLoader definitionLoader,
		[FromBody] IndexerTestRequest request,
		CancellationToken cancellationToken)
	{
		ValidateSettingsOrThrow(factory, definitionLoader, request.Implementation, request.DefinitionId, request.Settings);
		var definition = request.DefinitionId is { } definitionId ? definitionLoader.Load(definitionId)?.ToInfo() : null;
		return TypedResults.Ok(await RunTestAsync(factory, request.Implementation, request.Settings.GetRawText(), definition, cancellationToken));
	}

	private static async Task<Results<Ok<IndexerTestResult>, NotFound>> TestAsync(
		int id,
		SubmarineDbContext db,
		IIndexerFactory factory,
		IndexerDefinitionLoader definitionLoader,
		CancellationToken cancellationToken)
	{
		var indexer = await db.Indexers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (indexer is null)
		{
			return TypedResults.NotFound();
		}

		var definition = indexer.DefinitionId is { } definitionId ? definitionLoader.Load(definitionId)?.ToInfo() : null;
		return TypedResults.Ok(await RunTestAsync(factory, indexer.Implementation, indexer.SettingsJson, definition, cancellationToken));
	}

	private static async Task<Ok<IReadOnlyList<IndexerTestResult>>> TestAllAsync(
		SubmarineDbContext db,
		IIndexerFactory factory,
		IndexerDefinitionLoader definitionLoader,
		CancellationToken cancellationToken)
	{
		var indexers = await db.Indexers.AsNoTracking().ToListAsync(cancellationToken);
		var results = new List<IndexerTestResult>();

		foreach (var indexer in indexers)
		{
			var definition = indexer.DefinitionId is { } definitionId ? definitionLoader.Load(definitionId)?.ToInfo() : null;
			results.Add(await RunTestAsync(factory, indexer.Implementation, indexer.SettingsJson, definition, cancellationToken));
		}

		return TypedResults.Ok((IReadOnlyList<IndexerTestResult>)results);
	}

	private static async Task<Ok<BulkUpdateResult>> BulkUpdateAsync(
		SubmarineDbContext db,
		IIndexerProvider indexerProvider,
		IValidator<IndexerBulkRequest> validator,
		[FromBody] IndexerBulkRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var indexers = await db.Indexers.Include(entity => entity.Tags).Where(entity => request.Ids.Contains(entity.Id)).ToListAsync(cancellationToken);
		var tags = request.Tags is null ? [] : await db.Tags.Where(tag => request.Tags.TagIds.Contains(tag.Id)).ToListAsync(cancellationToken);

		foreach (var indexer in indexers)
		{
			if (request.EnableRss.HasValue)
			{
				indexer.EnableRss = request.EnableRss.Value;
			}

			if (request.EnableAutomaticSearch.HasValue)
			{
				indexer.EnableAutomaticSearch = request.EnableAutomaticSearch.Value;
			}

			if (request.EnableInteractiveSearch.HasValue)
			{
				indexer.EnableInteractiveSearch = request.EnableInteractiveSearch.Value;
			}

			if (request.Priority.HasValue)
			{
				indexer.Priority = request.Priority.Value;
			}

			if (request.ProxyId.HasValue)
			{
				indexer.ProxyId = request.ProxyId.Value == 0 ? null : request.ProxyId.Value;
			}

			if (request.Tags is not null)
			{
				ApplyTagChange(indexer, request.Tags, tags);
			}

			indexerProvider.InvalidateCapabilities(indexer.Id);
		}

		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(new BulkUpdateResult(indexers.Count));
	}

	private static async Task<Ok<BulkUpdateResult>> BulkDeleteAsync(
		SubmarineDbContext db,
		IIndexerProvider indexerProvider,
		[FromBody] BulkIdsRequest request,
		CancellationToken cancellationToken)
	{
		var indexers = await db.Indexers.Where(entity => request.Ids.Contains(entity.Id)).ToListAsync(cancellationToken);
		db.Indexers.RemoveRange(indexers);
		await db.SaveChangesAsync(cancellationToken);

		foreach (var indexer in indexers)
		{
			indexerProvider.InvalidateCapabilities(indexer.Id);
		}

		return TypedResults.Ok(new BulkUpdateResult(indexers.Count));
	}

	private static Ok<IReadOnlyList<IndexerCategoryDto>> CategoriesAsync()
		=> TypedResults.Ok((IReadOnlyList<IndexerCategoryDto>)[.. IndexerCategories.All.Select(ToCategoryDto)]);

	private static IndexerCategoryDto ToCategoryDto(IndexerCategory category)
		=> new(category.Id, category.Name, [.. category.SubCategories.Select(ToCategoryDto)]);

	private static async Task<IndexerTestResult> RunTestAsync(
		IIndexerFactory factory,
		IndexerImplementation implementation,
		string settingsJson,
		IndexerDefinitionInfo? definition,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var indexer = factory.Create(implementation, settingsJson, definition: definition);
			var capabilities = await indexer.GetCapabilitiesAsync(cancellationToken);
			await indexer.FetchRssAsync(cancellationToken);
			return new IndexerTestResult(true, null, EmptyErrors, ToSummary(capabilities));
		}
		catch (IndexerException exception)
		{
			return new IndexerTestResult(false, exception.Message, EmptyErrors, null);
		}
	}

	private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors = new Dictionary<string, string[]>();

	private static void ValidateSettingsOrThrow(
		IIndexerFactory factory,
		IndexerDefinitionLoader definitionLoader,
		IndexerImplementation implementation,
		string? definitionId,
		JsonElement settings)
	{
		var errors = factory.Validate(implementation, settings.GetRawText()).ToList();

		if (implementation == IndexerImplementation.CARDIGANN && errors.Count == 0 && definitionId is not null)
		{
			var definition = definitionLoader.Load(definitionId);
			if (definition is null)
			{
				errors.Add($"Cardigann definition '{definitionId}' is unknown");
			}
			else
			{
				var fields = JsonSerializer.Deserialize<CardigannSettings>(settings.GetRawText(), JsonOptions)?.Fields ?? new Dictionary<string, string>();
				foreach (var setting in definition.Settings.Where(setting => setting.Type?.ToLowerInvariant() is not ("info" or "checkbox") && setting.Default is null))
				{
					if (!fields.TryGetValue(setting.Name, out var value) || string.IsNullOrWhiteSpace(value))
					{
						errors.Add($"'{setting.Label ?? setting.Name}' is required");
					}
				}
			}
		}

		if (errors.Count == 0)
		{
			return;
		}

		throw new ValidationException(errors.Select(error => new ValidationFailure("settings", error)));
	}

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static async Task ApplyAsync(Indexer indexer, IndexerRequest request, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		indexer.Name = request.Name;
		indexer.Implementation = request.Implementation;
		indexer.DefinitionId = request.DefinitionId;
		indexer.Protocol = request.Protocol;
		indexer.BaseUrl = request.BaseUrl;
		indexer.SettingsJson = request.Settings.GetRawText();
		indexer.EnableRss = request.EnableRss;
		indexer.EnableAutomaticSearch = request.EnableAutomaticSearch;
		indexer.EnableInteractiveSearch = request.EnableInteractiveSearch;
		indexer.Priority = request.Priority;
		indexer.DownloadClientId = request.DownloadClientId;
		indexer.ProxyId = request.ProxyId;
		indexer.Categories = request.Categories ?? [];
		indexer.AnimeCategories = request.AnimeCategories ?? [];
		indexer.MinimumSeeders = request.MinimumSeeders;
		indexer.SeedRatio = request.SeedRatio;
		indexer.SeedTimeMinutes = request.SeedTimeMinutes;
		indexer.SeasonPackSeedTimeMinutes = request.SeasonPackSeedTimeMinutes;
		indexer.AnimeStandardFormatSearch = request.AnimeStandardFormatSearch;
		indexer.VipExpiration = request.VipExpiration;
		indexer.QueryLimit = request.QueryLimit;
		indexer.GrabLimit = request.GrabLimit;
		indexer.LimitsUnit = request.LimitsUnit;
		indexer.Redirect = request.Redirect;
		indexer.RequiredFlags = request.RequiredFlags ?? [];
		indexer.SeasonSearchMaximumSingleEpisodeAge = request.SeasonSearchMaximumSingleEpisodeAge;

		if (request.TagIds is not null)
		{
			var tags = await db.Tags.Where(tag => request.TagIds.Contains(tag.Id)).ToListAsync(cancellationToken);
			indexer.Tags.Clear();
			foreach (var tag in tags)
			{
				indexer.Tags.Add(tag);
			}
		}
	}

	private static void ApplyTagChange(Indexer indexer, TagChangeRequest change, List<Tag> tags)
	{
		switch (change.Mode)
		{
			case "add":
				foreach (var tag in tags.Where(tag => indexer.Tags.All(existing => existing.Id != tag.Id)))
				{
					indexer.Tags.Add(tag);
				}

				break;
			case "remove":
				foreach (var tag in tags)
				{
					var existing = indexer.Tags.FirstOrDefault(candidate => candidate.Id == tag.Id);
					if (existing is not null)
					{
						indexer.Tags.Remove(existing);
					}
				}

				break;
			default:
				indexer.Tags.Clear();
				foreach (var tag in tags)
				{
					indexer.Tags.Add(tag);
				}

				break;
		}
	}

	private static IndexerDto ToDto(Indexer indexer, IndexerStatus? status, IndexerCapabilityCache capabilityCache)
	{
		var summary = capabilityCache.TryGetAny(indexer.Id, out var capabilities) ? ToSummary(capabilities) : null;

		return new IndexerDto(
			indexer.Id,
			indexer.Name,
			indexer.Implementation,
			indexer.DefinitionId,
			indexer.Protocol,
			indexer.BaseUrl,
			JsonSerializer.Deserialize<JsonElement>(indexer.SettingsJson),
			indexer.EnableRss,
			indexer.EnableAutomaticSearch,
			indexer.EnableInteractiveSearch,
			indexer.Priority,
			indexer.DownloadClientId,
			indexer.ProxyId,
			indexer.Categories,
			indexer.AnimeCategories,
			indexer.MinimumSeeders,
			indexer.SeedRatio,
			indexer.SeedTimeMinutes,
			indexer.SeasonPackSeedTimeMinutes,
			indexer.AnimeStandardFormatSearch,
			[.. indexer.Tags.Select(tag => tag.Id)],
			new IndexerStatusSummary(status?.DisabledUntil, status?.MostRecentFailure, status?.EscalationLevel ?? 0),
			summary,
			indexer.VipExpiration,
			indexer.QueryLimit,
			indexer.GrabLimit,
			indexer.LimitsUnit,
			indexer.Redirect,
			indexer.RequiredFlags,
			indexer.SeasonSearchMaximumSingleEpisodeAge);
	}

	private static readonly System.Linq.Expressions.Expression<Func<IndexerHistory, IndexerHistoryDto>> HistoryDtoExpression
		= entry => new IndexerHistoryDto(entry.Id, entry.EventType, entry.Successful, entry.Query, entry.Categories, entry.Source, entry.ElapsedMs, entry.Date);

	private static IndexerCapabilitiesSummary ToSummary(IndexerCapabilities capabilities)
		=> new(capabilities.SearchAvailable, capabilities.TvSearchAvailable, capabilities.MovieSearchAvailable, capabilities.Categories.Count);

	private static IReadOnlyList<IndexerImplementationSchema> BuildImplementationSchema()
		=> new Dictionary<IndexerImplementation, Type>
		{
			[IndexerImplementation.TORZNAB] = typeof(TorznabSettings),
			[IndexerImplementation.NEWZNAB] = typeof(NewznabSettings),
			[IndexerImplementation.CARDIGANN] = typeof(CardigannSettings)
		}.Select(entry => new IndexerImplementationSchema(entry.Key, IndexerSettingsSchema.FieldsOf(entry.Value))).ToList();
}

/// <summary>Escalating failure state of an indexer.</summary>
public sealed record IndexerStatusSummary(DateTime? DisabledUntil, DateTime? MostRecentFailure, int EscalationLevel);

/// <summary>A lightweight summary of an indexer's reported capabilities.</summary>
public sealed record IndexerCapabilitiesSummary(bool SearchAvailable, bool TvSearchAvailable, bool MovieSearchAvailable, int CategoryCount);

/// <summary>A configured indexer.</summary>
public sealed record IndexerDto(
	int Id,
	string Name,
	IndexerImplementation Implementation,
	string? DefinitionId,
	Protocol Protocol,
	string BaseUrl,
	JsonElement Settings,
	bool EnableRss,
	bool EnableAutomaticSearch,
	bool EnableInteractiveSearch,
	int Priority,
	int? DownloadClientId,
	int? ProxyId,
	IReadOnlyList<int> Categories,
	IReadOnlyList<int> AnimeCategories,
	int? MinimumSeeders,
	double? SeedRatio,
	int? SeedTimeMinutes,
	int? SeasonPackSeedTimeMinutes,
	bool AnimeStandardFormatSearch,
	IReadOnlyList<int> TagIds,
	IndexerStatusSummary Status,
	IndexerCapabilitiesSummary? Capabilities,
	string? VipExpiration,
	int? QueryLimit,
	int? GrabLimit,
	IndexerLimitsUnit LimitsUnit,
	bool Redirect,
	IReadOnlyList<IndexerFlag> RequiredFlags,
	int? SeasonSearchMaximumSingleEpisodeAge);

/// <summary>Create or update request for an indexer.</summary>
public sealed record IndexerRequest(
	string Name,
	IndexerImplementation Implementation,
	string? DefinitionId,
	Protocol Protocol,
	string BaseUrl,
	JsonElement Settings,
	bool EnableRss,
	bool EnableAutomaticSearch,
	bool EnableInteractiveSearch,
	int Priority,
	int? DownloadClientId,
	int? ProxyId,
	List<int>? Categories,
	List<int>? AnimeCategories,
	int? MinimumSeeders,
	double? SeedRatio,
	int? SeedTimeMinutes,
	int? SeasonPackSeedTimeMinutes,
	bool AnimeStandardFormatSearch,
	List<int>? TagIds,
	string? VipExpiration,
	int? QueryLimit,
	int? GrabLimit,
	IndexerLimitsUnit LimitsUnit,
	bool Redirect,
	List<IndexerFlag>? RequiredFlags,
	int? SeasonSearchMaximumSingleEpisodeAge);

/// <summary>Test request for settings not yet saved.</summary>
public sealed record IndexerTestRequest(IndexerImplementation Implementation, string? DefinitionId, JsonElement Settings);

/// <summary>Result of a connection test.</summary>
public sealed record IndexerTestResult(bool IsValid, string? Message, IReadOnlyDictionary<string, string[]> FieldErrors, IndexerCapabilitiesSummary? Capabilities);

/// <summary>Tag change applied in a bulk request.</summary>
public sealed record TagChangeRequest(string Mode, List<int> TagIds);

/// <summary>Bulk edit request.</summary>
public sealed record IndexerBulkRequest(
	List<int> Ids,
	bool? EnableRss,
	bool? EnableAutomaticSearch,
	bool? EnableInteractiveSearch,
	int? Priority,
	int? ProxyId,
	TagChangeRequest? Tags);

/// <summary>A plain list of ids, used by bulk delete requests.</summary>
public sealed record BulkIdsRequest(List<int> Ids);

/// <summary>Result of a bulk update.</summary>
public sealed record BulkUpdateResult(int Updated);

/// <summary>One settings field descriptor.</summary>
public sealed record SettingsFieldSchema(string Name, string ClrType, bool Required, IReadOnlyList<string>? EnumValues, object? Default);

/// <summary>Settings schema for one indexer implementation.</summary>
public sealed record IndexerImplementationSchema(IndexerImplementation Implementation, IReadOnlyList<SettingsFieldSchema> Fields);

/// <summary>Implementation schemas plus every known Cardigann definition.</summary>
public sealed record IndexerSchemaResponse(IReadOnlyList<IndexerImplementationSchema> Implementations, IReadOnlyList<IndexerDefinitionInfo> CardigannDefinitions);

/// <summary>One indexer request history entry.</summary>
public sealed record IndexerHistoryDto(int Id, IndexerHistoryEventType EventType, bool Successful, string? Query, string? Categories, string? Source, int? ElapsedMs, DateTime Date);

/// <summary>A node of the standard Newznab category tree.</summary>
public sealed record IndexerCategoryDto(int Id, string Name, IReadOnlyList<IndexerCategoryDto> SubCategories);

/// <summary>Validator for <see cref="IndexerRequest" />.</summary>
public sealed class IndexerRequestValidator : AbstractValidator<IndexerRequest>
{
	/// <inheritdoc />
	public IndexerRequestValidator()
	{
		RuleFor(request => request.Name).NotEmpty().MaximumLength(256);
		RuleFor(request => request.Implementation).IsInEnum();
		RuleFor(request => request.Priority).InclusiveBetween(1, 50);
		RuleFor(request => request.DefinitionId).NotEmpty().When(request => request.Implementation == IndexerImplementation.CARDIGANN);
		RuleFor(request => request.BaseUrl).NotEmpty().When(request => request.Implementation != IndexerImplementation.CARDIGANN);
		RuleFor(request => request.QueryLimit).GreaterThan(0).When(request => request.QueryLimit.HasValue).WithMessage("Should be greater than zero");
		RuleFor(request => request.GrabLimit).GreaterThan(0).When(request => request.GrabLimit.HasValue).WithMessage("Should be greater than zero");
		RuleFor(request => request.Redirect).Equal(true).When(request => request.Protocol == Protocol.USENET).WithMessage("Redirect must be enabled for Usenet indexers");
	}
}

/// <summary>Validator for <see cref="IndexerBulkRequest" />.</summary>
public sealed class IndexerBulkRequestValidator : AbstractValidator<IndexerBulkRequest>
{
	/// <inheritdoc />
	public IndexerBulkRequestValidator()
	{
		RuleFor(request => request.Ids).NotNull().NotEmpty();
		RuleFor(request => request.Priority).InclusiveBetween(1, 50).When(request => request.Priority.HasValue);
		RuleFor(request => request.Tags)
			.Must(tags => tags is null || tags.Mode is "add" or "remove" or "replace")
			.WithMessage("Mode must be add, remove or replace");
	}
}

/// <summary>
///     Reflects indexer settings records into field schemas using nullable reference annotations to decide which
///     fields are required.
/// </summary>
internal static class IndexerSettingsSchema
{
	public static IReadOnlyList<SettingsFieldSchema> FieldsOf(Type settingsType)
	{
		var constructor = settingsType.GetConstructors()[0];
		var arguments = constructor.GetParameters()
			.Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : Default(parameter.ParameterType))
			.ToArray();
		var instance = constructor.Invoke(arguments);
		var context = new NullabilityInfoContext();

		return settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(property =>
			{
				var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
				var isEnum = propertyType.IsEnum;
				var required = property.PropertyType == typeof(string) && context.Create(property).ReadState == NullabilityState.NotNull;
				var clrType = isEnum ? "enum" : propertyType switch
				{
					{ } type when type == typeof(int) => "int",
					{ } type when type == typeof(double) => "double",
					{ } type when type == typeof(bool) => "bool",
					{ } type when type == typeof(string) => "string",
					_ => "object"
				};

				return new SettingsFieldSchema(
					ToCamelCase(property.Name),
					clrType,
					required,
					isEnum ? Enum.GetNames(propertyType) : null,
					property.GetValue(instance));
			})
			.ToList();
	}

	private static object? Default(Type type)
		=> type.IsValueType ? Activator.CreateInstance(type) : null;

	private static string ToCamelCase(string name)
		=> string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
