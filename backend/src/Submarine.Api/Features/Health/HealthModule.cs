using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Health;

/// <summary>
///     Current health issues.
/// </summary>
public sealed class HealthModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
		=> endpoints.MapGet("/api/v1/health", ListAsync);

	private static async Task<Ok<List<HealthIssueDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var issues = await db.HealthIssues.AsNoTracking()
			.OrderByDescending(x => x.Type)
			.ThenBy(x => x.Source)
			.ThenBy(x => x.Id)
			.Select(x => new HealthIssueDto(x.Id, x.Type.ToString(), x.Source, x.Message, x.WikiUrl))
			.ToListAsync(cancellationToken);
		return TypedResults.Ok(issues);
	}
}

/// <summary>A health issue.</summary>
/// <param name="Id">Row id.</param>
/// <param name="Type">Severity.</param>
/// <param name="Source">Check that reported the issue.</param>
/// <param name="Message">Issue description.</param>
/// <param name="WikiUrl">Documentation link.</param>
public sealed record HealthIssueDto(int Id, string Type, string Source, string Message, string? WikiUrl);
