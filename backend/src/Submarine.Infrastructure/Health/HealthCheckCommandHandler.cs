using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Microsoft.Extensions.Logging;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Runs every registered health check, diffs the results against the persisted
///     HealthIssue rows and publishes the change set.
/// </summary>
public sealed class HealthCheckCommandHandler(
	SubmarineDbContext db,
	IEnumerable<IHealthCheck> checks,
	IEventBus eventBus,
	TimeProvider timeProvider,
	ILogger<HealthCheckCommandHandler> logger) : ICommandHandler<HealthCheckCommand>
{
	private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

	/// <inheritdoc />
	public async Task ExecuteAsync(HealthCheckCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var snapshots = (await Task.WhenAll(checks.Select(check => RunCheckAsync(check, cancellationToken))))
			.SelectMany(x => x)
			.ToList();

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var existing = await db.HealthIssues.ToListAsync(cancellationToken);
		var snapshotKeys = snapshots.Select(x => x.Source + "|" + x.Message).ToHashSet(StringComparer.Ordinal);

		var removed = existing.Where(x => !snapshotKeys.Contains(x.Source + "|" + x.Message)).ToList();
		var existingKeys = existing.Select(x => x.Source + "|" + x.Message).ToHashSet(StringComparer.Ordinal);
		var added = snapshots.Where(x => !existingKeys.Contains(x.Source + "|" + x.Message)).ToList();

		db.HealthIssues.RemoveRange(removed);
		foreach (var issue in existing.IntersectBy(snapshots.Select(x => x.Source + "|" + x.Message), x => x.Source + "|" + x.Message))
		{
			var snapshot = snapshots.First(x => x.Source == issue.Source && x.Message == issue.Message);
			issue.Type = snapshot.Type;
			issue.WikiUrl = snapshot.WikiUrl;
			issue.UpdatedAt = now;
		}

		foreach (var snapshot in added)
		{
			db.HealthIssues.Add(new HealthIssue
			{
				Type = snapshot.Type,
				Source = snapshot.Source,
				Message = snapshot.Message,
				WikiUrl = snapshot.WikiUrl,
				CreatedAt = now,
				UpdatedAt = now
			});
		}

		await db.SaveChangesAsync(cancellationToken);

		await eventBus.PublishAsync(new HealthIssuesChangedEvent(snapshots, added, removed.Select(ToSnapshot).ToList()),
			cancellationToken);
		await context.ReportProgressAsync(100, $"{snapshots.Count} issues", cancellationToken);
	}

	private async Task<IReadOnlyList<HealthIssueSnapshot>> RunCheckAsync(IHealthCheck check, CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(CheckTimeout);
		try
		{
			return await check.CheckAsync(timeout.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning("Health check {Check} timed out after {Timeout}", check.GetType().Name, CheckTimeout);
			return [];
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Health check {Check} failed", check.GetType().Name);
			return [];
		}
	}

	private static HealthIssueSnapshot ToSnapshot(HealthIssue issue)
		=> new(issue.Type, issue.Source, issue.Message, issue.WikiUrl);
}
