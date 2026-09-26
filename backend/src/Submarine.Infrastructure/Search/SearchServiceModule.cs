using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Registers release search, matching, decision context building and the interactive/automatic search
///     orchestrators.
/// </summary>
public sealed class SearchServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddMemoryCache();
		services.AddScoped<ReleaseSearchService>();
		services.AddScoped<ReleaseMatcher>();
		services.AddScoped<DecisionContextFactory>();
		services.AddScoped<EpisodeSearchPlanner>();
		services.AddScoped<InteractiveSearchService>();
		services.AddScoped<AutomaticSearchService>();
		services.AddSingleton<ReleaseResultCache>();
	}
}
