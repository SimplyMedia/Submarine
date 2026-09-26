using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Auth;

/// <summary>
///     Registers the auth config provider and the password hasher.
/// </summary>
public static class AuthServiceCollectionExtensions
{
	/// <summary>
	///     Add Submarine auth services.
	/// </summary>
	public static IServiceCollection AddSubmarineAuth(this IServiceCollection services)
	{
		services.AddMemoryCache();
		services.TryAddSingleton(new LoggingLevelSwitch());
		services.TryAddSingleton<IAuthConfigProvider, AuthConfigProvider>();
		services.TryAddSingleton<ICookiePrincipalValidator, CookiePrincipalValidator>();
		services.TryAddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
		return services;
	}
}
