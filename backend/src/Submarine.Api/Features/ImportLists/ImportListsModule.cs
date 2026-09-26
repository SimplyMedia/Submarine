using System.Text.Json;
using FluentValidation;
using Submarine.Api.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.ImportLists;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.ImportLists;

/// <summary>
///     Import list management, testing, preview, schema and exclusions.
/// </summary>
public sealed class ImportListsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/import-lists");
		group.MapGet("/", ListAsync);
		group.MapGet("/schema", SchemaAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
		group.MapPost("/{id:int}/test", TestAsync);
		group.MapGet("/{id:int}/preview", PreviewAsync);

		var exclusions = endpoints.MapGroup("/api/v1/import-list-exclusions");
		exclusions.MapGet("/", ListExclusionsAsync);
		exclusions.MapPost("/", CreateExclusionAsync);
		exclusions.MapPut("/{id:int}", UpdateExclusionAsync);
		exclusions.MapDelete("/{id:int}", DeleteExclusionAsync);
	}

	private static async Task<Ok<List<ImportListDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var lists = await db.ImportLists.AsNoTracking().Include(x => x.Tags).OrderBy(x => x.Name).ToListAsync(cancellationToken);
		var statuses = await db.ImportListStatuses.AsNoTracking().ToDictionaryAsync(x => x.ImportListId, cancellationToken);
		return TypedResults.Ok(lists.Select(x => ImportListMapper.ToDto(x, statuses.GetValueOrDefault(x.Id))).ToList());
	}

	private static Ok<IReadOnlyList<ImportListTypeSchema>> SchemaAsync()
		=> TypedResults.Ok(ImportListSchemas.All);

	private static async Task<Ok<ImportListDto>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var list = await db.ImportLists.AsNoTracking().Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Import list {id} not found");
		var status = await db.ImportListStatuses.AsNoTracking().FirstOrDefaultAsync(x => x.ImportListId == id, cancellationToken);
		return TypedResults.Ok(ImportListMapper.ToDto(list, status));
	}

	private static async Task<Created<ImportListDto>> CreateAsync(
		SubmarineDbContext db,
		IValidator<SaveImportListRequest> validator,
		[FromBody] SaveImportListRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var list = new ImportList
		{
			Name = request.Name,
			Type = request.Type,
			Enable = request.Enable ?? true,
			EnableAutomaticAdd = request.EnableAutomaticAdd ?? true,
			SearchOnAdd = request.SearchOnAdd ?? false,
			SettingsJson = JsonSerializer.Serialize(request.Settings, Submarine.Core.Common.SubmarineJson.Default),
			MediaKind = request.MediaKind,
			QualityProfileId = request.QualityProfileId,
			LanguageProfileId = request.LanguageProfileId,
			RootFolderId = request.RootFolderId,
			Monitor = request.Monitor ?? MonitorNewItems.ALL,
			MinimumAvailability = request.MinimumAvailability,
			SeriesType = request.SeriesType,
			SeasonFolder = request.SeasonFolder ?? true,
			Tags = await LoadTagsAsync(db, request.TagIds, cancellationToken)
		};
		db.ImportLists.Add(list);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Created($"/api/v1/import-lists/{list.Id}", ImportListMapper.ToDto(list));
	}

	private static async Task<Ok<ImportListDto>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<SaveImportListRequest> validator,
		[FromBody] SaveImportListRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var list = await db.ImportLists.Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Import list {id} not found");

		list.Name = request.Name;
		list.Type = request.Type;
		list.Enable = request.Enable ?? list.Enable;
		list.EnableAutomaticAdd = request.EnableAutomaticAdd ?? list.EnableAutomaticAdd;
		list.SearchOnAdd = request.SearchOnAdd ?? list.SearchOnAdd;
		list.SettingsJson = JsonSerializer.Serialize(request.Settings, Submarine.Core.Common.SubmarineJson.Default);
		list.MediaKind = request.MediaKind;
		list.QualityProfileId = request.QualityProfileId;
		list.LanguageProfileId = request.LanguageProfileId;
		list.RootFolderId = request.RootFolderId;
		list.Monitor = request.Monitor ?? list.Monitor;
		list.MinimumAvailability = request.MinimumAvailability;
		list.SeriesType = request.SeriesType;
		list.SeasonFolder = request.SeasonFolder ?? list.SeasonFolder;
		list.Tags = await LoadTagsAsync(db, request.TagIds, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ImportListMapper.ToDto(list));
	}

	private static async Task<NoContent> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var list = await db.ImportLists.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Import list {id} not found");
		db.ImportLists.Remove(list);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<ImportListTestDto>> TestAsync(
		int id,
		SubmarineDbContext db,
		IEnumerable<IImportList> implementations,
		CancellationToken cancellationToken)
	{
		var list = await db.ImportLists.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Import list {id} not found");
		var implementation = implementations.FirstOrDefault(x => x.Handles(list.Type))
			?? throw new InvalidOperationException($"No implementation for import list type {list.Type}");
		try
		{
			var items = await implementation.FetchAsync(list, cancellationToken);
			return TypedResults.Ok(new ImportListTestDto(true, items.Count, null));
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return TypedResults.Ok(new ImportListTestDto(false, 0, exception.Message));
		}
	}

	private static async Task<Ok<List<ImportListPreviewItemDto>>> PreviewAsync(
		int id,
		SubmarineDbContext db,
		IEnumerable<IImportList> implementations,
		CancellationToken cancellationToken)
	{
		var list = await db.ImportLists.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Import list {id} not found");
		var implementation = implementations.FirstOrDefault(x => x.Handles(list.Type))
			?? throw new InvalidOperationException($"No implementation for import list type {list.Type}");
		var items = await implementation.FetchAsync(list, cancellationToken);

		var excludedTvdb = await db.ImportListExclusions.Where(x => x.TvdbId != null).Select(x => x.TvdbId!.Value).ToListAsync(cancellationToken);
		var excludedTmdb = await db.ImportListExclusions.Where(x => x.TmdbId != null).Select(x => x.TmdbId!.Value).ToListAsync(cancellationToken);
		var existingTvdb = await db.Series.Select(x => x.TvdbId).ToListAsync(cancellationToken);
		var existingTmdb = await db.Movies.Select(x => x.TmdbId).ToListAsync(cancellationToken);

		return TypedResults.Ok(items.Select(item =>
		{
			var status = list.MediaKind == MediaKind.SERIES
				? item.TvdbId is null
					? "unresolved"
					: excludedTvdb.Contains(item.TvdbId.Value)
						? "excluded"
						: existingTvdb.Contains(item.TvdbId.Value)
							? "exists"
							: "new"
				: item.TmdbId is null
					? "unresolved"
					: excludedTmdb.Contains(item.TmdbId.Value)
						? "excluded"
						: existingTmdb.Contains(item.TmdbId.Value)
							? "exists"
							: "new";
			return new ImportListPreviewItemDto(item.TvdbId, item.TmdbId, item.ImdbId, item.Title, item.Year, status);
		}).ToList());
	}

	private static async Task<Ok<List<ImportListExclusionDto>>> ListExclusionsAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var exclusions = await db.ImportListExclusions.AsNoTracking().OrderBy(x => x.Title).ToListAsync(cancellationToken);
		return TypedResults.Ok(exclusions.Select(x => new ImportListExclusionDto(
			x.Id,
			x.TvdbId,
			x.TmdbId,
			x.Title,
			x.Year)).ToList());
	}

	private static async Task<Created<ImportListExclusionDto>> CreateExclusionAsync(
		SubmarineDbContext db,
		IValidator<SaveImportListExclusionRequest> validator,
		[FromBody] SaveImportListExclusionRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var exclusion = new ImportListExclusion
		{
			TvdbId = request.TvdbId,
			TmdbId = request.TmdbId,
			Title = request.Title,
			Year = request.Year
		};
		db.ImportListExclusions.Add(exclusion);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Created($"/api/v1/import-list-exclusions/{exclusion.Id}", new ImportListExclusionDto(exclusion.Id, exclusion.TvdbId, exclusion.TmdbId, exclusion.Title, exclusion.Year));
	}

	private static async Task<Ok<ImportListExclusionDto>> UpdateExclusionAsync(
		int id,
		SubmarineDbContext db,
		IValidator<SaveImportListExclusionRequest> validator,
		[FromBody] SaveImportListExclusionRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var exclusion = await db.ImportListExclusions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Import list exclusion {id} not found");
		exclusion.TvdbId = request.TvdbId;
		exclusion.TmdbId = request.TmdbId;
		exclusion.Title = request.Title;
		exclusion.Year = request.Year;
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(new ImportListExclusionDto(exclusion.Id, exclusion.TvdbId, exclusion.TmdbId, exclusion.Title, exclusion.Year));
	}

	private static async Task<NoContent> DeleteExclusionAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var exclusion = await db.ImportListExclusions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Import list exclusion {id} not found");
		db.ImportListExclusions.Remove(exclusion);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<List<Tag>> LoadTagsAsync(SubmarineDbContext db, List<int>? tagIds, CancellationToken cancellationToken)
	{
		if (tagIds is not { Count: > 0 })
		{
			return [];
		}

		var found = await db.Tags.Where(x => tagIds.Contains(x.Id)).ToListAsync(cancellationToken);
		if (found.Count != tagIds.Distinct().Count())
		{
			throw new KeyNotFoundException("One or more tags not found");
		}

		return found;
	}
}

/// <summary>Create or update import list request.</summary>
/// <param name="Name">Name.</param>
/// <param name="Type">List implementation.</param>
/// <param name="Enable">Whether the list syncs.</param>
/// <param name="EnableAutomaticAdd">Whether found items are added.</param>
/// <param name="SearchOnAdd">Search after adding.</param>
/// <param name="Settings">Implementation settings.</param>
/// <param name="MediaKind">Series or movies.</param>
/// <param name="QualityProfileId">Quality profile for added items.</param>
/// <param name="LanguageProfileId">Language profile for added items.</param>
/// <param name="RootFolderId">Root folder for added items.</param>
/// <param name="Monitor">How new seasons are monitored.</param>
/// <param name="MinimumAvailability">Minimum availability for added movies.</param>
/// <param name="SeriesType">Series type for added series.</param>
/// <param name="SeasonFolder">Whether added series use season folders.</param>
/// <param name="TagIds">Tags applied to added items.</param>
public sealed record SaveImportListRequest(
	string Name,
	ImportListType Type,
	bool? Enable,
	bool? EnableAutomaticAdd,
	bool? SearchOnAdd,
	JsonElement Settings,
	MediaKind MediaKind,
	int? QualityProfileId,
	int? LanguageProfileId,
	int? RootFolderId,
	MonitorNewItems? Monitor,
	MinimumAvailability? MinimumAvailability,
	SeriesType? SeriesType,
	bool? SeasonFolder,
	List<int>? TagIds);

/// <summary>Validator for <see cref="SaveImportListRequest" />.</summary>
public sealed class SaveImportListRequestValidator : AbstractValidator<SaveImportListRequest>
{
	/// <inheritdoc />
	public SaveImportListRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
		RuleFor(x => x.Type).IsInEnum();
		RuleFor(x => x.MediaKind).IsInEnum();
		RuleFor(x => x.Monitor).IsInEnum().When(x => x.Monitor.HasValue);
		RuleFor(x => x.MinimumAvailability).IsInEnum().When(x => x.MinimumAvailability.HasValue);
		RuleFor(x => x.SeriesType).IsInEnum().When(x => x.SeriesType.HasValue);
		RuleFor(x => x.QualityProfileId).GreaterThan(0).When(x => x.QualityProfileId.HasValue);
		RuleFor(x => x.LanguageProfileId).GreaterThan(0).When(x => x.LanguageProfileId.HasValue);
		RuleFor(x => x.RootFolderId).GreaterThan(0).When(x => x.RootFolderId.HasValue);
		RuleForEach(x => x.TagIds).GreaterThan(0);
		RuleFor(x => x.Settings)
			.Must(x => x.ValueKind is JsonValueKind.Object)
			.WithMessage("Settings must be a JSON object")
			.When(x => x.Settings.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null));
		RuleFor(x => x)
			.Must(x => x.Type != ImportListType.TMDB_LIST
				|| x.Settings.ValueKind != JsonValueKind.Object
				|| x.Settings.TryGetProperty("listId", out _))
			.WithMessage("The listId setting is required for TMDB lists")
			.When(x => x.Type == ImportListType.TMDB_LIST);
	}
}

/// <summary>Create or update import list exclusion request.</summary>
/// <param name="TvdbId">TVDB id, for series.</param>
/// <param name="TmdbId">TMDB id, for movies.</param>
/// <param name="Title">Display title.</param>
/// <param name="Year">Release year.</param>
public sealed record SaveImportListExclusionRequest(int? TvdbId, int? TmdbId, string Title, int? Year);

/// <summary>Validator for <see cref="SaveImportListExclusionRequest" />.</summary>
public sealed class SaveImportListExclusionRequestValidator : AbstractValidator<SaveImportListExclusionRequest>
{
	/// <inheritdoc />
	public SaveImportListExclusionRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(512);
		RuleFor(x => x)
			.Must(x => x.TvdbId is > 0 || x.TmdbId is > 0)
			.WithMessage("A tvdbId or tmdbId is required");
	}
}

/// <summary>Result of an import list test.</summary>
/// <param name="Success">Whether the fetch worked.</param>
/// <param name="ItemCount">Number of items fetched.</param>
/// <param name="Message">Error message, when the fetch failed.</param>
public sealed record ImportListTestDto(bool Success, int ItemCount, string? Message);

/// <summary>One preview item.</summary>
/// <param name="TvdbId">TVDB id.</param>
/// <param name="TmdbId">TMDB id.</param>
/// <param name="ImdbId">IMDB id.</param>
/// <param name="Title">Title.</param>
/// <param name="Year">Year.</param>
/// <param name="Status">New, exists, excluded or unresolved.</param>
public sealed record ImportListPreviewItemDto(
	int? TvdbId,
	int? TmdbId,
	string? ImdbId,
	string Title,
	int? Year,
	string Status);

/// <summary>An import list exclusion.</summary>
/// <param name="Id">Exclusion id.</param>
/// <param name="TvdbId">TVDB id, for series.</param>
/// <param name="TmdbId">TMDB id, for movies.</param>
/// <param name="Title">Display title.</param>
/// <param name="Year">Release year.</param>
public sealed record ImportListExclusionDto(int Id, int? TvdbId, int? TmdbId, string Title, int? Year);

/// <summary>Maps import list entities to API records.</summary>
public static class ImportListMapper
{
	/// <summary>Map an import list entity, with its runtime status when known.</summary>
	public static ImportListDto ToDto(ImportList list, ImportListStatus? status = null)
	{
		using var settings = JsonDocument.Parse(
			string.IsNullOrWhiteSpace(list.SettingsJson) ? "{}" : list.SettingsJson,
			new JsonDocumentOptions { AllowTrailingCommas = true });
		return new ImportListDto(
			list.Id,
			list.Name,
			list.Type,
			list.Enable,
			list.EnableAutomaticAdd,
			list.SearchOnAdd,
			settings.RootElement.Clone(),
			list.MediaKind,
			list.QualityProfileId,
			list.LanguageProfileId,
			list.RootFolderId,
			list.Monitor,
			list.MinimumAvailability,
			list.SeriesType,
			list.SeasonFolder,
			[.. list.Tags.Select(x => x.Id)],
			(int)ImportListSchemas.MinRefreshInterval(list.Type).TotalMinutes,
			new ImportListStatusSummary(status?.LastSyncAt, status?.DisabledUntil, status?.EscalationLevel ?? 0));
	}
}

/// <summary>Runtime sync and backoff state of an import list.</summary>
/// <param name="LastSyncAt">Last successful fetch, null when never synced.</param>
/// <param name="DisabledUntil">Disabled by backoff until this time, null when not disabled.</param>
/// <param name="EscalationLevel">Current backoff escalation level.</param>
public sealed record ImportListStatusSummary(DateTime? LastSyncAt, DateTime? DisabledUntil, int EscalationLevel);

/// <summary>An import list.</summary>
/// <param name="Id">List id.</param>
/// <param name="Name">Name.</param>
/// <param name="Type">List implementation.</param>
/// <param name="Enable">Whether the list syncs.</param>
/// <param name="EnableAutomaticAdd">Whether found items are added.</param>
/// <param name="SearchOnAdd">Search after adding.</param>
/// <param name="Settings">Implementation settings.</param>
/// <param name="MediaKind">Series or movies.</param>
/// <param name="QualityProfileId">Quality profile for added items.</param>
/// <param name="LanguageProfileId">Language profile for added items.</param>
/// <param name="RootFolderId">Root folder for added items.</param>
/// <param name="Monitor">How new seasons are monitored.</param>
/// <param name="MinimumAvailability">Minimum availability for added movies.</param>
/// <param name="SeriesType">Series type for added series.</param>
/// <param name="SeasonFolder">Whether added series use season folders.</param>
/// <param name="TagIds">Tags applied to added items.</param>
/// <param name="MinRefreshIntervalMinutes">Minimum minutes between automatic scheduled fetches of this list type.</param>
/// <param name="Status">Runtime sync and backoff state.</param>
public sealed record ImportListDto(
	int Id,
	string Name,
	ImportListType Type,
	bool Enable,
	bool EnableAutomaticAdd,
	bool SearchOnAdd,
	JsonElement Settings,
	MediaKind MediaKind,
	int? QualityProfileId,
	int? LanguageProfileId,
	int? RootFolderId,
	MonitorNewItems Monitor,
	MinimumAvailability? MinimumAvailability,
	SeriesType? SeriesType,
	bool SeasonFolder,
	List<int> TagIds,
	int MinRefreshIntervalMinutes,
	ImportListStatusSummary Status);
