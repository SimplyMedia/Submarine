using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Default ICommandContext persisting progress on the Command row.
/// </summary>
public sealed class CommandContext(
	int commandId,
	SubmarineDbContext db,
	IEventBus eventBus,
	TimeProvider timeProvider) : ICommandContext
{
	/// <inheritdoc />
	public int CommandId => commandId;

	/// <inheritdoc />
	public async Task ReportProgressAsync(int percent, string? message = null, CancellationToken cancellationToken = default)
	{
		var row = await db.Commands.FindAsync([commandId], cancellationToken);
		if (row is null)
		{
			return;
		}

		row.Progress = Math.Clamp(percent, 0, 100);
		row.Message = message;
		row.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
		await db.SaveChangesAsync(cancellationToken);

		await eventBus.PublishAsync(
			new CommandUpdated(row.Id, row.Name, row.Status, row.Progress, row.Message, row.StartedAt, row.EndedAt),
			cancellationToken);
	}
}
