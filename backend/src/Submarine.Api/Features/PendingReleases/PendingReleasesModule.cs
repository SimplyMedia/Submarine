using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.PendingReleases;

/// <summary>
///     Lists and removes releases held pending a delay or availability window.
/// </summary>
public sealed class PendingReleasesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/pending-releases");
		group.MapGet("/", ListAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<PendingReleaseDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var entries = db.PendingReleases.AsNoTracking().OrderByDescending(entry => entry.Added);
		return TypedResults.Ok(await PagedResult<PendingReleaseDto>.CreateAsync(entries.Select(ToDto), query, cancellationToken));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var entry = await db.PendingReleases.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
		if (entry is null)
		{
			return TypedResults.NotFound();
		}

		db.PendingReleases.Remove(entry);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static readonly System.Linq.Expressions.Expression<Func<Core.Entities.PendingRelease, PendingReleaseDto>> ToDto
		= entry => new PendingReleaseDto(entry.Id, entry.Title, entry.SeriesId, entry.MovieId, entry.EpisodeIds, entry.Reason, entry.Added);
}

/// <summary>A release held back until its delay or availability window clears.</summary>
public sealed record PendingReleaseDto(
	int Id,
	string Title,
	int? SeriesId,
	int? MovieId,
	IReadOnlyList<int> EpisodeIds,
	PendingReleaseReason Reason,
	DateTime Added);
