using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Series;

/// <summary>
///     Series library endpoints.
/// </summary>
public sealed class SeriesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/series");
		group.MapGet("/", ListAsync);
		group.MapGet("/lookup", LookupAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/editor", BulkUpdateAsync);
		group.MapDelete("/editor", BulkDeleteAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
		group.MapGet("/{id:int}/folder", FolderAsync);
		group.MapPost("/{id:int}/refresh", RefreshAsync);
		group.MapPost("/{id:int}/rescan", RescanAsync);
		group.MapPost("/{id:int}/search", SearchAsync);
	}

	private static async Task<Ok<PagedResult<SeriesListItemDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		[FromQuery] bool? monitored,
		[FromQuery] SeriesType? type,
		[FromQuery] string? term,
		[FromQuery] int? tagId,
		[FromQuery] int? rootFolderId,
		[FromQuery] SeriesStatus? status,
		CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var series = db.Series.AsNoTracking().AsQueryable();
		if (monitored.HasValue)
		{
			series = series.Where(x => x.Monitored == monitored.Value);
		}

		if (type.HasValue)
		{
			series = series.Where(x => x.Type == type.Value);
		}

		if (status.HasValue)
		{
			series = series.Where(x => x.Status == status.Value);
		}

		if (tagId.HasValue)
		{
			series = series.Where(x => x.Tags.Any(t => t.Id == tagId.Value));
		}

		if (rootFolderId.HasValue)
		{
			series = series.Where(x => x.Versions.Any(v => v.RootFolderId == rootFolderId.Value));
		}

		if (!string.IsNullOrWhiteSpace(term))
		{
			var cleaned = TitleNormalizer.CleanTitle(term);
			series = series.Where(x => x.CleanTitle.Contains(cleaned) || x.TvdbId.ToString() == cleaned);
		}

		// Custom sorts that need episode aggregates run on the entity query, everything
		// else is sorted by PagedResult on the projected DTO.
		var sortKey = query.SortKey?.Trim().ToLowerInvariant();
		var descending = PagingQuery.IsDescending(query.SortDirection);
		if (sortKey is "nextairing" or "episodes" or "episodecount" or "sizeondisk")
		{
			series = sortKey switch
			{
				"nextairing" => series.ApplyOrder(
					x => x.Episodes
						.Where(e => e.Monitored && e.AirDateUtc != null && e.AirDateUtc > now)
						.Min(e => e.AirDateUtc),
					descending),
				"sizeondisk" => series.ApplyOrder(
					x => x.Episodes.Where(e => e.Monitored).SelectMany(e => e.Files).Sum(f => (long?)f.Size) ?? 0,
					descending),
				_ => series.ApplyOrder(x => x.Episodes.Count, descending)
			};
		}
		else
		{
			series = PagedResult<Submarine.Core.Entities.Series>.ApplySort(series, query.SortKey, query.SortDirection, defaultToId: true);
		}

		query = query with { SortKey = null };

		var page = await PagedResult<SeriesListItemDto>.CreateAsync(series.Select(x => new SeriesListItemDto(
			x.Id,
			x.TvdbId,
			x.TmdbId,
			x.ImdbId,
			x.Title,
			x.SortTitle,
			x.Overview,
			x.Network,
			x.Runtime,
			x.Year,
			x.PosterUrl,
			x.BackdropUrl,
			x.Status,
			x.Type,
			x.MetadataProvider,
			x.Numbering,
			x.Monitored,
			x.MonitorNewItems,
			x.SeasonFolder,
			x.Genres,
			x.Certification,
			x.FirstAired,
			x.CreatedAt,
			x.Episodes
				.Where(e => e.Monitored && e.AirDateUtc != null && e.AirDateUtc > now)
				.Min(e => e.AirDateUtc),
			x.Episodes
				.Where(e => e.Monitored && e.AirDateUtc != null && e.AirDateUtc <= now)
				.Max(e => e.AirDateUtc),
			x.Tags.Select(t => t.Id).ToList(),
			x.Versions.Select(v => new VersionDto(
				v.Id,
				v.Name,
				v.SeriesId,
				v.MovieId,
				v.QualityProfileId,
				v.LanguageProfileId,
				v.RootFolderId,
				db.RootFolders.Where(r => r.Id == v.RootFolderId).Select(r => r.Path).FirstOrDefault(),
				v.Path,
				v.Monitored)).ToList(),
			new SeriesStatisticsDto(
				x.Episodes.Count(e => e.Monitored),
				x.Episodes.Count(e => e.Monitored && e.Files.Any()),
				x.Episodes.Count(),
				x.Episodes.Where(e => e.Monitored).SelectMany(e => e.Files).Sum(f => (long?)f.Size) ?? 0,
				x.Episodes.Count(e => e.Monitored) == 0
					? 100.0
					: Math.Round(100.0
						* x.Episodes.Count(e => e.Monitored && e.Files.Any())
						/ x.Episodes.Count(e => e.Monitored), 1)
			))), query, cancellationToken);

		return TypedResults.Ok(page);
	}

	private static async Task<Results<Ok<IEnumerable<SeriesLookupDto>>, ProblemHttpResult>> LookupAsync(
		SubmarineDbContext db,
		IMetadataClient metadata,
		[FromQuery] string term,
		[FromQuery] MetadataProvider? provider,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(term))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A search term is required");
		}

		var hits = await metadata.SearchSeriesAsync(term, provider ?? MetadataProvider.TVDB, cancellationToken);
		var tvdbIds = hits.Select(x => x.TvdbId).OfType<int>().ToList();
		var existing = await db.Series
			.Where(x => tvdbIds.Contains(x.TvdbId))
			.ToDictionaryAsync(x => x.TvdbId, x => x.Id, cancellationToken);
		return TypedResults.Ok(hits.Select(x => new SeriesLookupDto(
			x.TvdbId,
			x.TmdbId,
			x.ImdbId,
			x.Title,
			x.Year,
			x.Overview,
			x.PosterUrl,
			x.Status,
			x.Provider,
			x.TvdbId is { } tvdbId && existing.TryGetValue(tvdbId, out var id) ? id : null)));
	}

	private static async Task<Ok<SeriesDetailDto>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var series = await db.Series.AsNoTracking()
			.Include(x => x.Tags)
			.Include(x => x.Seasons)
			.Include(x => x.Episodes).ThenInclude(x => x.Files)
			.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Series {id} not found");
		var rootPaths = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
		var titles = await db.AlternativeTitles
			.Where(x => x.SeriesId == id)
			.Select(x => x.Title)
			.ToListAsync(cancellationToken);
		return TypedResults.Ok(SeriesMapper.ToDetail(series, rootPaths, titles));
	}

	private static async Task<Created<SeriesDetailDto>> CreateAsync(
		SubmarineDbContext db,
		LibraryAdder adder,
		IValidator<AddSeriesRequest> validator,
		[FromBody] AddSeriesRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var series = await adder.AddSeriesAsync(
			new AddSeriesOptions(
				request.TvdbId,
				request.TmdbId,
				request.MetadataProvider ?? Submarine.Core.Enums.MetadataProvider.TVDB,
				request.Title,
				request.RootFolderId,
				request.SeriesType ?? SeriesType.STANDARD,
				request.Numbering ?? SeriesNumbering.AIRED,
				request.SeasonFolder ?? true,
				request.Monitored ?? true,
				request.MonitorOption ?? AddMonitorOption.ALL,
				request.MonitorSpecials ?? false,
				request.MonitorNewItems ?? MonitorNewItems.ALL,
				request.TagIds ?? [],
				[.. request.Versions.Select(v => new VersionOptions(
					string.IsNullOrWhiteSpace(v.Name) ? "main" : v.Name,
					v.QualityProfileId,
					v.LanguageProfileId,
					v.RootFolderId))],
				request.SearchOnAdd ?? false),
			cancellationToken);
		return TypedResults.Created($"/api/v1/series/{series.Id}", await ReloadDetailAsync(db, series.Id, cancellationToken));
	}

	private static async Task<Ok<SeriesDetailDto>> UpdateAsync(
		SubmarineDbContext db,
		LibraryMutator mutator,
		IValidator<UpdateSeriesRequest> validator,
		int id,
		[FromBody] UpdateSeriesRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await mutator.UpdateSeriesAsync(
			id,
			new UpdateSeriesOptions(
				request.Monitored,
				request.SeasonFolder,
				request.SeriesType,
				request.Numbering,
				request.MonitorNewItems,
				request.TagIds,
				request.RootFolderId,
				request.MoveFiles ?? false,
				[.. (request.Versions ?? []).Select(v => new UpdateVersionOptions(v.Id, v.Name, v.QualityProfileId, v.LanguageProfileId, v.Path))]),
			cancellationToken);
		return TypedResults.Ok(await ReloadDetailAsync(db, id, cancellationToken));
	}

	private static async Task<NoContent> DeleteAsync(
		LibraryMutator mutator,
		int id,
		[FromQuery] bool deleteFiles = false,
		[FromQuery] bool addImportListExclusion = false,
		CancellationToken cancellationToken = default)
	{
		await mutator.DeleteSeriesAsync(id, deleteFiles, addImportListExclusion, cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<BulkUpdateResultDto>> BulkUpdateAsync(
		LibraryMutator mutator,
		IValidator<SeriesEditorRequest> validator,
		[FromBody] SeriesEditorRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var updated = await mutator.BulkUpdateSeriesAsync(
			new BulkUpdateSeriesOptions(
				request.Ids,
				request.Monitored,
				request.SeriesType,
				request.SeasonFolder,
				request.MonitorNewItems,
				request.QualityProfileId,
				request.LanguageProfileId,
				request.RootFolderId,
				request.MoveFiles ?? false,
				request.Tags is null ? null : new TagOperation(
					request.Tags.Mode switch
					{
						"add" => TagOperationMode.ADD,
						"remove" => TagOperationMode.REMOVE,
						_ => TagOperationMode.REPLACE
					},
					request.Tags.TagIds)),
			cancellationToken);
		return TypedResults.Ok(new BulkUpdateResultDto(updated));
	}

	private static async Task<NoContent> BulkDeleteAsync(
		LibraryMutator mutator,
		IValidator<BulkDeleteRequest> validator,
		[FromBody] BulkDeleteRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await mutator.BulkDeleteSeriesAsync(request.Ids, request.DeleteFiles ?? false, request.AddImportListExclusion ?? false, cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<SeriesFolderDto>> FolderAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var version = await db.MediaVersions.AsNoTracking()
			.Where(x => x.SeriesId == id)
			.OrderBy(x => x.Id)
			.Select(x => new { x.RootFolderId, x.Path })
			.FirstOrDefaultAsync(cancellationToken);
		if (version is null)
		{
			throw new KeyNotFoundException($"Series {id} not found");
		}

		var rootPath = await db.RootFolders
			.Where(x => x.Id == version.RootFolderId)
			.Select(x => x.Path)
			.FirstOrDefaultAsync(cancellationToken);
		var folder = rootPath is null ? null : Path.Combine(rootPath, version.Path);
		return TypedResults.Ok(new SeriesFolderDto(folder, folder is not null && Directory.Exists(folder)));
	}

	private static async Task<Created<Command>> RefreshAsync(int id, ICommandQueue queue, CancellationToken cancellationToken)
		=> await EnqueueAsync(queue, new RefreshSeriesCommand(id), cancellationToken);

	private static async Task<Created<Command>> RescanAsync(int id, ICommandQueue queue, CancellationToken cancellationToken)
		=> await EnqueueAsync(queue, new RescanSeriesCommand(id), cancellationToken);

	private static async Task<Created<Command>> SearchAsync(int id, ICommandQueue queue, CancellationToken cancellationToken)
		=> await EnqueueAsync(queue, new SeriesSearchCommand(id), cancellationToken);

	internal static async Task<Created<Command>> EnqueueAsync(ICommandQueue queue, ICommand command, CancellationToken cancellationToken)
	{
		var row = await queue.EnqueueAsync(command, CommandTrigger.MANUAL, CommandPriority.NORMAL, cancellationToken);
		return TypedResults.Created($"/api/v1/commands/{row.Id}", row);
	}

	private static async Task<SeriesDetailDto?> ReloadDetailAsync(SubmarineDbContext db, int id, CancellationToken cancellationToken)
	{
		var series = await db.Series.AsNoTracking()
			.Include(x => x.Tags)
			.Include(x => x.Seasons)
			.Include(x => x.Episodes).ThenInclude(x => x.Files)
			.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (series is null)
		{
			return null;
		}

		var rootPaths = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
		var titles = await db.AlternativeTitles
			.Where(x => x.SeriesId == id)
			.Select(x => x.Title)
			.ToListAsync(cancellationToken);
		return SeriesMapper.ToDetail(series, rootPaths, titles);
	}
}

/// <summary>Bulk operation result.</summary>
/// <param name="Updated">Number of items updated.</param>
public sealed record BulkUpdateResultDto(int Updated);

/// <summary>Ordering helpers for aggregate sort keys.</summary>
public static class SeriesQueryableExtensions
{
	/// <summary>Order by a selector ascending or descending.</summary>
	public static IQueryable<T> ApplyOrder<T, TKey>(
		this IQueryable<T> source,
		Expression<Func<T, TKey>> selector,
		bool descending)
		=> descending
			? source.OrderByDescending(selector)
			: source.OrderBy(selector);
}
