using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     Deletes recycle bin entries older than the configured retention. A retention of 0 keeps everything.
/// </summary>
public sealed class RecycleBinCleanupCommandHandler(
	SubmarineDbContext db,
	IRecycleBinService recycleBinService,
	TimeProvider timeProvider) : ICommandHandler<RecycleBinCleanupCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RecycleBinCleanupCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		if (mediaManagement.RecycleBinCleanupDays <= 0 || string.IsNullOrWhiteSpace(mediaManagement.RecycleBinPath))
		{
			return;
		}

		var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-mediaManagement.RecycleBinCleanupDays);
		var deleted = recycleBinService.CleanupOlderThan(mediaManagement.RecycleBinPath, cutoff);
		await context.ReportProgressAsync(100, $"Deleted {deleted} recycle bin item(s)", cancellationToken);
	}
}
