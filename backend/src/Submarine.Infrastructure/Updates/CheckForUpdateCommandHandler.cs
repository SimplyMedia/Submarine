using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Notifications;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Updates;

/// <summary>
///     Runs an update check, upserts a health notice when a newer release exists
///     and lets notifications with OnApplicationUpdate fire.
/// </summary>
public sealed class CheckForUpdateCommandHandler(
	SubmarineDbContext db,
	IUpdateChecker updateChecker,
	IEventBus eventBus,
	TimeProvider timeProvider) : ICommandHandler<CheckForUpdateCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(CheckForUpdateCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var info = await updateChecker.GetLatestAsync(bypassCache: true, cancellationToken);
		if (!info.UpdateAvailable)
		{
			// Clear a stale notice when the running version is current again.
			var removed = await db.HealthIssues
				.Where(x => x.Source == "Updates")
				.ExecuteDeleteAsync(cancellationToken);
			if (removed > 0)
			{
				var current = await db.HealthIssues.AsNoTracking()
					.Select(x => new HealthIssueSnapshot(x.Type, x.Source, x.Message, x.WikiUrl))
					.ToListAsync(cancellationToken);
				await eventBus.PublishAsync(new HealthIssuesChangedEvent(current, [], []), cancellationToken);
			}

			await context.ReportProgressAsync(100, "No update available", cancellationToken);
			return;
		}

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var existing = await db.HealthIssues.FirstOrDefaultAsync(x => x.Source == "Updates", cancellationToken);
		if (existing is null)
		{
			db.HealthIssues.Add(new HealthIssue
			{
				Type = HealthIssueType.NOTICE,
				Source = "Updates",
				Message = $"New update available: {info.LatestVersion}",
				WikiUrl = info.ReleaseNotesUrl,
				CreatedAt = now,
				UpdatedAt = now
			});
		}
		else
		{
			existing.Message = $"New update available: {info.LatestVersion}";
			existing.WikiUrl = info.ReleaseNotesUrl;
			existing.UpdatedAt = now;
		}

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new ApplicationUpdateAvailableEvent(
			info.CurrentVersion,
			info.LatestVersion!,
			info.ReleaseNotesUrl!), cancellationToken);
		await context.ReportProgressAsync(100, $"Update {info.LatestVersion} available", cancellationToken);
	}
}
