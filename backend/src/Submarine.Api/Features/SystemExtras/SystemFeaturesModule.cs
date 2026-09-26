using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Api.Modules;
using Submarine.Core.Modules;
using Submarine.Infrastructure.Backups;
using Submarine.Infrastructure.DownloadClients;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Notifications;
using Submarine.Infrastructure.Updates;

namespace Submarine.Api.Features.SystemExtras;

/// <summary>
///     Wires the infrastructure feature stacks owned by this slice: notifications,
///     health checks, updates and backups. Endpoint modules stay registration free.
/// </summary>
public sealed class SystemFeaturesModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddSubmarineDownloadClients();
		services.AddSubmarineNotifications();
		services.AddSubmarineHealth();
		services.AddSubmarineUpdates();
		services.AddSubmarineBackups();
	}
}
