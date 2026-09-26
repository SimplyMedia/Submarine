using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.SystemExtras;

/// <summary>
///     Restart, shutdown and disk space reporting.
/// </summary>
public sealed class SystemExtrasModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/system");
		group.MapPost("/restart", RestartAsync);
		group.MapPost("/shutdown", ShutdownAsync);
		group.MapGet("/disk-space", DiskSpaceAsync);
	}

	private static Accepted<RestartDto> RestartAsync(IHostApplicationLifetime lifetime, HttpContext context)
		=> StopAfterResponse(lifetime, context, new RestartDto(true));

	private static Accepted<ShutdownDto> ShutdownAsync(IHostApplicationLifetime lifetime, HttpContext context)
		=> StopAfterResponse(lifetime, context, new ShutdownDto(true));

	private static Accepted<T> StopAfterResponse<T>(IHostApplicationLifetime lifetime, HttpContext context, T body)
	{
		context.Response.OnCompleted(() =>
		{
			lifetime.StopApplication();
			return Task.CompletedTask;
		});
		return TypedResults.Accepted((string?)null, body);
	}

	private static async Task<Ok<DiskSpaceReportDto>> DiskSpaceAsync(
		SubmarineDbContext db,
		IWebHostEnvironment environment,
		CancellationToken cancellationToken)
	{
		var folders = await db.RootFolders.AsNoTracking().ToListAsync(cancellationToken);
		var appData = new DiskSpaceDto(
			environment.ContentRootPath,
			DiskSpace.Query(environment.ContentRootPath)?.FreeBytes,
			DiskSpace.Query(environment.ContentRootPath)?.TotalBytes);
		var rootFolders = folders
			.Select(folder => DiskSpace.Query(folder.Path) is { } space
				? new DiskSpaceDto(folder.Path, space.FreeBytes, space.TotalBytes)
				: new DiskSpaceDto(folder.Path, null, null))
			.ToList();
		return TypedResults.Ok(new DiskSpaceReportDto(appData, rootFolders));
	}
}

/// <summary>Restart response.</summary>
/// <param name="RestartRequired">Whether a container restart is expected.</param>
public sealed record RestartDto(bool RestartRequired);

/// <summary>Shutdown response.</summary>
/// <param name="Shutdown">Whether the application will stop.</param>
public sealed record ShutdownDto(bool Shutdown);

/// <summary>Free and total space of one path.</summary>
/// <param name="Path">Path on disk.</param>
/// <param name="FreeBytes">Free bytes, null when unavailable.</param>
/// <param name="TotalBytes">Total bytes, null when unavailable.</param>
public sealed record DiskSpaceDto(string Path, long? FreeBytes, long? TotalBytes);

/// <summary>Disk space report.</summary>
/// <param name="AppData">Space of the application data drive.</param>
/// <param name="RootFolders">Space of each root folder drive.</param>
public sealed record DiskSpaceReportDto(DiskSpaceDto AppData, IReadOnlyList<DiskSpaceDto> RootFolders);
