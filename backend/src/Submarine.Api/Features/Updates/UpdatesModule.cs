using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Updates;

namespace Submarine.Api.Features.Updates;

/// <summary>
///     Update availability and release history from the GitHub releases API.
/// </summary>
public sealed class UpdatesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/updates");
		group.MapGet("/", GetAsync);
		group.MapGet("/releases", GetReleasesAsync);
	}

	private static async Task<Ok<UpdateDto>> GetAsync(
		SubmarineDbContext db,
		IUpdateChecker updateChecker,
		CancellationToken cancellationToken)
	{
		var branch = await db.GeneralConfig.AsNoTracking().Select(x => x.Branch).SingleAsync(cancellationToken);
		var info = await updateChecker.GetLatestAsync(branch, cancellationToken: cancellationToken);
		return TypedResults.Ok(new UpdateDto(
			info.CurrentVersion,
			info.LatestVersion,
			info.ReleaseNotesUrl,
			info.UpdateAvailable,
			File.Exists("/.dockerenv"),
			info.CheckFailed));
	}

	private static async Task<Ok<IReadOnlyList<ReleaseInfo>>> GetReleasesAsync(
		IUpdateChecker updateChecker,
		CancellationToken cancellationToken)
		=> TypedResults.Ok(await updateChecker.GetReleasesAsync(cancellationToken: cancellationToken));
}

/// <summary>Update availability.</summary>
/// <param name="Current">Version of the running instance.</param>
/// <param name="Latest">Newest published version for the configured branch, null when the check failed or none exists yet.</param>
/// <param name="ReleaseNotesUrl">Url of the newest release notes.</param>
/// <param name="UpdateAvailable">Whether a newer release exists.</param>
/// <param name="IsDocker">Whether the instance runs inside Docker.</param>
/// <param name="CheckFailed">Whether the update check failed, distinct from a check that succeeded and found no matching release.</param>
public sealed record UpdateDto(
	string Current,
	string? Latest,
	string? ReleaseNotesUrl,
	bool UpdateAvailable,
	bool IsDocker,
	bool CheckFailed);
