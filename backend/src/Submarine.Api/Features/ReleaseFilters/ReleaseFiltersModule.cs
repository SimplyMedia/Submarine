using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.ReleaseFilters;

/// <summary>
///     Release filter CRUD.
/// </summary>
public sealed class ReleaseFiltersModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/release-filters");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<ReleaseFilterResource>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var filters = await db.ReleaseFilters.OrderBy(filter => filter.Id).ToListAsync(cancellationToken);

		return TypedResults.Ok(filters.Select(ReleaseFilterResource.FromEntity).ToList());
	}

	private static async Task<Results<Ok<ReleaseFilterResource>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var filter = await db.ReleaseFilters.AsNoTracking().FirstOrDefaultAsync(filter => filter.Id == id, cancellationToken);

		return filter is null ? TypedResults.NotFound() : TypedResults.Ok(ReleaseFilterResource.FromEntity(filter));
	}

	private static async Task<Created<ReleaseFilterResource>> CreateAsync(
		SubmarineDbContext db,
		ReleaseFilterRequest request,
		CancellationToken cancellationToken)
	{
		var filter = new Core.Entities.ReleaseFilter();
		ApplyRequest(filter, request);

		db.ReleaseFilters.Add(filter);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/release-filters/{filter.Id}", ReleaseFilterResource.FromEntity(filter));
	}

	private static async Task<Results<Ok<ReleaseFilterResource>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		ReleaseFilterRequest request,
		CancellationToken cancellationToken)
	{
		var filter = await db.ReleaseFilters.FirstOrDefaultAsync(filter => filter.Id == id, cancellationToken);
		if (filter is null)
		{
			return TypedResults.NotFound();
		}

		ApplyRequest(filter, request);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(ReleaseFilterResource.FromEntity(filter));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var filter = await db.ReleaseFilters.FirstOrDefaultAsync(filter => filter.Id == id, cancellationToken);
		if (filter is null)
		{
			return TypedResults.NotFound();
		}

		db.ReleaseFilters.Remove(filter);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static void ApplyRequest(Core.Entities.ReleaseFilter filter, ReleaseFilterRequest request)
	{
		filter.Field = request.Field;
		filter.Values = [.. request.Values];
		filter.Mode = request.Mode;
		filter.Tier = request.Tier;
	}
}

/// <summary>A release filter resource.</summary>
/// <param name="Id">Id.</param>
/// <param name="Field">Field the values match against.</param>
/// <param name="Values">Values to match, case-insensitive.</param>
/// <param name="Mode">How matches are treated.</param>
/// <param name="Tier">Tier for prefer filters, lower tiers win.</param>
public sealed record ReleaseFilterResource(
	int Id,
	Submarine.Core.Enums.ReleaseFilterField Field,
	IReadOnlyList<string> Values,
	Submarine.Core.Enums.ReleaseFilterMode Mode,
	int Tier)
{
	/// <summary>Maps an entity to the resource.</summary>
	public static ReleaseFilterResource FromEntity(Core.Entities.ReleaseFilter filter)
		=> new(filter.Id, filter.Field, filter.Values, filter.Mode, filter.Tier);
}

/// <summary>Create or update request for a release filter.</summary>
/// <param name="Field">Field the values match against.</param>
/// <param name="Values">Values to match, case-insensitive.</param>
/// <param name="Mode">How matches are treated.</param>
/// <param name="Tier">Tier for prefer filters, lower tiers win.</param>
public sealed record ReleaseFilterRequest(
	Submarine.Core.Enums.ReleaseFilterField Field,
	IReadOnlyList<string> Values,
	Submarine.Core.Enums.ReleaseFilterMode Mode,
	int Tier);
