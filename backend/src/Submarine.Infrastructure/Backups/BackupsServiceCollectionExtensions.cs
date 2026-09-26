using Microsoft.Extensions.DependencyInjection;

namespace Submarine.Infrastructure.Backups;

/// <summary>
///     Dependency injection registration for the backup service.
/// </summary>
public static class BackupsServiceCollectionExtensions
{
	/// <summary>
	///     Registers the backup service.
	/// </summary>
	public static IServiceCollection AddSubmarineBackups(this IServiceCollection services)
		=> services.AddSingleton<BackupService>();
}
