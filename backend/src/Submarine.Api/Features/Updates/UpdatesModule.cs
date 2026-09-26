using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Updates;

namespace Submarine.Api.Features.Updates;

/// <summary>
///     Update availability from the GitHub releases API.
/// </summary>
public sealed class UpdatesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
		=> endpoints.MapGet("/api/v1/updates", GetAsync);

	private static async Task<Ok<UpdateDto>> GetAsync(
		IUpdateChecker updateChecker,
		CancellationToken cancellationToken)
	{
		var info = await updateChecker.GetLatestAsync(cancellationToken: cancellationToken);
		return TypedResults.Ok(new UpdateDto(
			info.CurrentVersion,
			info.LatestVersion,
			info.ReleaseNotesUrl,
			info.UpdateAvailable,
			File.Exists("/.dockerenv"),
			info.CheckFailed));
	}
}

/// <summary>Update availability.</summary>
/// <param name="Current">Version of the running instance.</param>
/// <param name="Latest">Newest published version, null when the check failed.</param>
/// <param name="ReleaseNotesUrl">Url of the newest release notes.</param>
/// <param name="UpdateAvailable">Whether a newer release exists.</param>
/// <param name="IsDocker">Whether the instance runs inside Docker.</param>
/// <param name="CheckFailed">Whether the update check failed, distinct from a check that succeeded and found no update.</param>
public sealed record UpdateDto(
	string Current,
	string? Latest,
	string? ReleaseNotesUrl,
	bool UpdateAvailable,
	bool IsDocker,
	bool CheckFailed);
