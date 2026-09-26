using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Backups;

/// <summary>
///     Creates a backup, marked MANUAL or SCHEDULED based on how the command was enqueued.
/// </summary>
public sealed class BackupCommandHandler(
	SubmarineDbContext db,
	BackupService backupService) : ICommandHandler<BackupCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(BackupCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var trigger = await db.Commands.AsNoTracking()
			.Where(x => x.Id == context.CommandId)
			.Select(x => x.Trigger)
			.FirstOrDefaultAsync(cancellationToken);
		var kind = trigger == CommandTrigger.SCHEDULED ? BackupKind.SCHEDULED : BackupKind.MANUAL;

		await context.ReportProgressAsync(10, "Creating backup", cancellationToken);
		var entry = await backupService.CreateAsync(kind, cancellationToken);
		await context.ReportProgressAsync(100, entry.Name, cancellationToken);
	}
}
