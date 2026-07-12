using Submarine.Api.Services;

namespace Submarine.Api.Jobs;

/// <summary>
///     Periodically creates a database and configuration backup, applying retention
/// </summary>
public sealed class BackupJob : IScheduledJob
{
	public string Name => "Backup";

	public TimeSpan Interval => TimeSpan.FromDays(7);

	public async Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		var service = scopedProvider.GetRequiredService<BackupService>();
		var logger = scopedProvider.GetRequiredService<ILogger<BackupJob>>();

		try
		{
			await service.CreateAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Creating scheduled backup failed");
		}
	}
}
