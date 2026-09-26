using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Registers indexer provisioning, backoff tracking and request history recording.
/// </summary>
public sealed class IndexerManagementServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<IndexerCapabilityCache>();
		services.AddScoped<IIndexerProvider, IndexerProvider>();
		services.AddScoped<IIndexerStatusService, IndexerStatusService>();
		services.AddScoped<IIndexerLimitService, IndexerLimitService>();
		services.AddScoped<IndexerHistoryRecorder>();
	}
}
