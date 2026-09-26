using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Blocklist;

/// <summary>
///     Blocked releases that must not be grabbed again.
/// </summary>
public sealed class BlocklistModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/blocklist");
		group.MapGet("/", ListAsync);
		group.MapDelete("/all", DeleteAllAsync);
		group.MapDelete("/bulk", BulkDeleteAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<BlocklistItemDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var items = db.BlocklistItems
			.AsNoTracking()
			.Include(x => x.Series)
			.Include(x => x.Movie)
			.Include(x => x.Indexer)
			.OrderByDescending(x => x.Date);

		var paged = await PagedResult<BlocklistItem>.CreateAsync(items, query, cancellationToken);
		return TypedResults.Ok(new PagedResult<BlocklistItemDto>([.. paged.Items.Select(ToDto)], paged.Page, paged.PageSize, paged.TotalCount));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var item = await db.BlocklistItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (item is null)
		{
			return TypedResults.NotFound();
		}

		db.BlocklistItems.Remove(item);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Ok<BulkBlocklistDeleteResult>> BulkDeleteAsync(
		SubmarineDbContext db,
		IValidator<BulkBlocklistDeleteRequest> validator,
		[FromBody] BulkBlocklistDeleteRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var deleted = await db.BlocklistItems.Where(x => request.Ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
		return TypedResults.Ok(new BulkBlocklistDeleteResult(deleted));
	}

	private static async Task<Accepted<Command>> DeleteAllAsync(ICommandQueue commandQueue, CancellationToken cancellationToken)
	{
		var command = await commandQueue.EnqueueAsync(new ClearBlocklistCommand(), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		return TypedResults.Accepted((string?)null, command);
	}

	private static BlocklistItemDto ToDto(BlocklistItem item)
		=> new(
			item.Id,
			item.ReleaseTitle,
			item.Guid,
			item.Protocol.ToString(),
			item.IndexerId,
			item.Indexer?.Name,
			item.SeriesId,
			item.Series?.Title,
			item.MovieId,
			item.Movie?.Title,
			item.EpisodeIds,
			item.Reason,
			item.Size,
			item.Date);
}

/// <summary>A blocked release.</summary>
/// <param name="Id">Id.</param>
/// <param name="ReleaseTitle">Release title.</param>
/// <param name="Guid">Indexer guid, if known.</param>
/// <param name="Protocol">Release protocol.</param>
/// <param name="IndexerId">Indexer id.</param>
/// <param name="IndexerName">Indexer name.</param>
/// <param name="SeriesId">Related series.</param>
/// <param name="SeriesTitle">Related series title.</param>
/// <param name="MovieId">Related movie.</param>
/// <param name="MovieTitle">Related movie title.</param>
/// <param name="EpisodeIds">Related episode ids.</param>
/// <param name="Reason">Why the release was blocked.</param>
/// <param name="Size">Release size in bytes.</param>
/// <param name="Date">When the release was blocked.</param>
public sealed record BlocklistItemDto(
	int Id,
	string ReleaseTitle,
	string? Guid,
	string Protocol,
	int? IndexerId,
	string? IndexerName,
	int? SeriesId,
	string? SeriesTitle,
	int? MovieId,
	string? MovieTitle,
	List<int> EpisodeIds,
	string Reason,
	long? Size,
	DateTime Date);

/// <summary>Bulk blocklist delete request.</summary>
/// <param name="Ids">Ids to remove.</param>
public sealed record BulkBlocklistDeleteRequest(List<int> Ids);

/// <summary>Result of a bulk blocklist delete.</summary>
/// <param name="Removed">Number of rows removed.</param>
public sealed record BulkBlocklistDeleteResult(int Removed);

/// <summary>Validator for <see cref="BulkBlocklistDeleteRequest" />.</summary>
public sealed class BulkBlocklistDeleteRequestValidator : AbstractValidator<BulkBlocklistDeleteRequest>
{
	/// <inheritdoc />
	public BulkBlocklistDeleteRequestValidator()
	{
		RuleFor(x => x.Ids).NotNull().NotEmpty();
		RuleForEach(x => x.Ids).GreaterThan(0);
	}
}
