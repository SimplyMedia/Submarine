using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     Empties the blocklist.
/// </summary>
public sealed class ClearBlocklistCommandHandler(SubmarineDbContext db) : ICommandHandler<ClearBlocklistCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(ClearBlocklistCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		await db.BlocklistItems.ExecuteDeleteAsync(cancellationToken);
	}
}
