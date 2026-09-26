using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Deletes completed, failed and cancelled commands older than seven days, and log rows
///     older than fourteen days.
/// </summary>
public sealed class CommandCleanupCommandHandler(SubmarineDbContext db, LogDbContext logDb, TimeProvider timeProvider)
	: ICommandHandler<CommandCleanupCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(CommandCleanupCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var commandCutoff = now.AddDays(-7);
		var removed = await db.Commands
			.Where(x => x.CreatedAt < commandCutoff
				&& (x.Status == CommandStatus.COMPLETED
					|| x.Status == CommandStatus.FAILED
					|| x.Status == CommandStatus.CANCELLED))
			.ExecuteDeleteAsync(cancellationToken);

		var logCutoff = now.AddDays(-14);
		var removedLogs = await logDb.Logs.Where(x => x.Time < logCutoff).ExecuteDeleteAsync(cancellationToken);

		await context.ReportProgressAsync(100, $"Pruned {removed} command rows, {removedLogs} log rows", cancellationToken);
	}
}
