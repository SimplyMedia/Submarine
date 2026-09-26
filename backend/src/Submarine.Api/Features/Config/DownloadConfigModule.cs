using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>
///     Download handling configuration.
/// </summary>
public sealed class DownloadConfigModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/config/download");
		group.MapGet("/", GetAsync);
		group.MapPut("/", PutAsync);
	}

	private static async Task<Ok<DownloadConfigResource>> GetAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(DownloadConfigResource.FromEntity(await db.DownloadConfig.AsNoTracking().SingleAsync(cancellationToken)));

	private static async Task<Ok<DownloadConfigResource>> PutAsync(
		SubmarineDbContext db,
		IValidator<DownloadConfigResource> validator,
		DownloadConfigResource request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var config = await db.DownloadConfig.SingleAsync(cancellationToken);

		config.EnableCompletedDownloadHandling = request.EnableCompletedDownloadHandling;
		config.RemoveCompletedDownloads = request.RemoveCompletedDownloads;
		config.EnableFailedDownloadHandling = request.EnableFailedDownloadHandling;
		config.RedownloadFailedReleases = request.RedownloadFailedReleases;
		config.RemoveFailedDownloads = request.RemoveFailedDownloads;
		config.CheckForFinishedDownloadInterval = request.CheckForFinishedDownloadInterval;

		// The scheduler enqueues DownloadMonitor on this interval; 0 disables it entirely.
		var downloadMonitorTask = await db.ScheduledTasks.FirstOrDefaultAsync(x => x.Name == "DownloadMonitor", cancellationToken);
		if (downloadMonitorTask is not null)
		{
			downloadMonitorTask.IntervalMinutes = request.CheckForFinishedDownloadInterval;
		}

		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(DownloadConfigResource.FromEntity(config));
	}
}

/// <summary>Download handling configuration resource.</summary>
/// <param name="EnableCompletedDownloadHandling">Import completed downloads automatically.</param>
/// <param name="RemoveCompletedDownloads">Remove imported downloads from the client.</param>
/// <param name="EnableFailedDownloadHandling">Handle failed downloads.</param>
/// <param name="RedownloadFailedReleases">Automatically redownload failed releases.</param>
/// <param name="RemoveFailedDownloads">Remove failed downloads from the client.</param>
/// <param name="CheckForFinishedDownloadInterval">Minutes between finished download checks.</param>
public sealed record DownloadConfigResource(
	bool EnableCompletedDownloadHandling,
	bool RemoveCompletedDownloads,
	bool EnableFailedDownloadHandling,
	bool RedownloadFailedReleases,
	bool RemoveFailedDownloads,
	int CheckForFinishedDownloadInterval)
{
	/// <summary>Maps the singleton to the resource.</summary>
	public static DownloadConfigResource FromEntity(Core.Entities.DownloadConfig config)
		=> new(
			config.EnableCompletedDownloadHandling,
			config.RemoveCompletedDownloads,
			config.EnableFailedDownloadHandling,
			config.RedownloadFailedReleases,
			config.RemoveFailedDownloads,
			config.CheckForFinishedDownloadInterval);
}

/// <summary>Validator for <see cref="DownloadConfigResource" /> PUT requests.</summary>
public sealed class DownloadConfigResourceValidator : AbstractValidator<DownloadConfigResource>
{
	/// <inheritdoc />
	public DownloadConfigResourceValidator()
	{
		RuleFor(x => x.CheckForFinishedDownloadInterval).GreaterThanOrEqualTo(0);
	}
}
