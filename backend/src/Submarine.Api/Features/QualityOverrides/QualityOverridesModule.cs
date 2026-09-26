using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.QualityOverrides;

/// <summary>
///     User defined release group quality source overrides.
/// </summary>
public sealed class QualityOverridesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/quality-overrides");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<ReleaseGroupOverrideResource>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var overrides = await db.ReleaseGroupQualityOverrides
			.OrderBy(overrideRow => overrideRow.ReleaseGroup)
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(overrides.Select(ReleaseGroupOverrideResource.FromEntity).ToList());
	}

	private static async Task<Results<Ok<ReleaseGroupOverrideResource>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var overrideRow = await db.ReleaseGroupQualityOverrides
			.AsNoTracking()
			.FirstOrDefaultAsync(overrideRow => overrideRow.Id == id, cancellationToken);

		return overrideRow is null ? TypedResults.NotFound() : TypedResults.Ok(ReleaseGroupOverrideResource.FromEntity(overrideRow));
	}

	private static async Task<Created<ReleaseGroupOverrideResource>> CreateAsync(
		SubmarineDbContext db,
		QualityOverrideSource source,
		ReleaseGroupOverrideRequest request,
		CancellationToken cancellationToken)
	{
		await EnsureUniqueGroupAsync(db, request.ReleaseGroup, null, cancellationToken);

		var overrideRow = new Core.Entities.ReleaseGroupQualityOverride
		{
			ReleaseGroup = request.ReleaseGroup,
			Source = request.Source
		};

		db.ReleaseGroupQualityOverrides.Add(overrideRow);
		await db.SaveChangesAsync(cancellationToken);
		source.Invalidate();

		return TypedResults.Created($"/api/v1/quality-overrides/{overrideRow.Id}", ReleaseGroupOverrideResource.FromEntity(overrideRow));
	}

	private static async Task<Results<Ok<ReleaseGroupOverrideResource>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		QualityOverrideSource source,
		ReleaseGroupOverrideRequest request,
		CancellationToken cancellationToken)
	{
		await EnsureUniqueGroupAsync(db, request.ReleaseGroup, id, cancellationToken);

		var overrideRow = await db.ReleaseGroupQualityOverrides
			.FirstOrDefaultAsync(overrideRow => overrideRow.Id == id, cancellationToken);
		if (overrideRow is null)
		{
			return TypedResults.NotFound();
		}

		overrideRow.ReleaseGroup = request.ReleaseGroup;
		overrideRow.Source = request.Source;
		await db.SaveChangesAsync(cancellationToken);
		source.Invalidate();

		return TypedResults.Ok(ReleaseGroupOverrideResource.FromEntity(overrideRow));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(
		int id,
		SubmarineDbContext db,
		QualityOverrideSource source,
		CancellationToken cancellationToken)
	{
		var overrideRow = await db.ReleaseGroupQualityOverrides
			.FirstOrDefaultAsync(overrideRow => overrideRow.Id == id, cancellationToken);
		if (overrideRow is null)
		{
			return TypedResults.NotFound();
		}

		db.ReleaseGroupQualityOverrides.Remove(overrideRow);
		await db.SaveChangesAsync(cancellationToken);
		source.Invalidate();

		return TypedResults.NoContent();
	}

	private static async Task EnsureUniqueGroupAsync(
		SubmarineDbContext db,
		string releaseGroup,
		int? excludeId,
		CancellationToken cancellationToken)
	{
		var exists = await db.ReleaseGroupQualityOverrides
			.AnyAsync(overrideRow => overrideRow.ReleaseGroup == releaseGroup && (excludeId == null || overrideRow.Id != excludeId),
				cancellationToken);

		if (exists)
		{
			throw new Submarine.Core.Common.ConflictException($"An override for release group '{releaseGroup}' already exists");
		}
	}
}

/// <summary>A release group quality source override.</summary>
/// <param name="Id">Id.</param>
/// <param name="ReleaseGroup">Release group name, unique.</param>
/// <param name="Source">Quality source assumed for this group.</param>
public sealed record ReleaseGroupOverrideResource(int Id, string ReleaseGroup, QualitySource Source)
{
	/// <summary>Maps an entity to the resource.</summary>
	public static ReleaseGroupOverrideResource FromEntity(Core.Entities.ReleaseGroupQualityOverride overrideRow)
		=> new(overrideRow.Id, overrideRow.ReleaseGroup, overrideRow.Source);
}

/// <summary>Create or update request for a release group quality source override.</summary>
/// <param name="ReleaseGroup">Release group name, unique.</param>
/// <param name="Source">Quality source assumed for this group.</param>
public sealed record ReleaseGroupOverrideRequest(string ReleaseGroup, QualitySource Source);
