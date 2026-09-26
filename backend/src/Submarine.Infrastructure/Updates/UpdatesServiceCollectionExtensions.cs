using Microsoft.Extensions.DependencyInjection;

namespace Submarine.Infrastructure.Updates;

/// <summary>
///     Dependency injection registration for the update checker.
/// </summary>
public static class UpdatesServiceCollectionExtensions
{
	/// <summary>
	///     Registers the update checker.
	/// </summary>
	public static IServiceCollection AddSubmarineUpdates(this IServiceCollection services)
	{
		services.AddHttpClient(GitHubUpdateChecker.HttpClientName, client =>
		{
			client.Timeout = TimeSpan.FromSeconds(10);
			client.DefaultRequestHeaders.UserAgent.ParseAdd("Submarine");
			client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		});
		services.AddMemoryCache();
		services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

		return services;
	}
}
