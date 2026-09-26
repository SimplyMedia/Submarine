using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Submarine.Api.Auth;
using Submarine.Api.Modules;
using Submarine.Core.Modules;

namespace Submarine.Api.Features.Compat.Shared;

public sealed class CompatModule : IEndpointModule, IServiceModule
{
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddScoped<ICompatStatusProjector, CompatStatusProjector>();
		services.AddScoped<CompatVersionSelection>();
		services.AddSingleton<ICompatRealtimePublisher, CompatRealtimePublisher>();
		services.AddAuthorization(options => options.AddPolicy(
			CompatRoutes.AuthorizationPolicy,
			policy => policy.AddAuthenticationSchemes(AuthenticationSetup.ApiKeyScheme).RequireAuthenticatedUser()));
	}

	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr", "v3");
		MapFacade(endpoints, "radarr", "v3");
		MapFacade(endpoints, "prowlarr", "v1");

		endpoints.MapFallback("/compat/{**path}", () => Results.Json(
			new { message = "Not Found" }, CompatJson.Options, statusCode: StatusCodes.Status404NotFound))
			.RequireAuthorization(CompatRoutes.AuthorizationPolicy)
			.ExcludeFromDescription();
		endpoints.MapHub<SonarrCompatHub>("/compat/sonarr/signalr/messages")
			.RequireAuthorization(CompatRoutes.AuthorizationPolicy)
			.ExcludeFromDescription();
		endpoints.MapHub<RadarrCompatHub>("/compat/radarr/signalr/messages")
			.RequireAuthorization(CompatRoutes.AuthorizationPolicy)
			.ExcludeFromDescription();
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade, string apiVersion)
	{
		var group = CompatRoutes.CreateFacadeGroup(endpoints, facade);
		group.MapGet("/api", () => Results.Json(new CompatApiDiscoveryDto(apiVersion, []), CompatJson.Options));
		group.MapGet("/api/system/status", StatusAsync);
		group.MapGet($"/api/{apiVersion}/system/status", StatusAsync);
		group.MapFallback("/{**path}", () => Results.Json(
			new { message = "Not Found" }, CompatJson.Options, statusCode: StatusCodes.Status404NotFound))
			.RequireAuthorization(CompatRoutes.AuthorizationPolicy)
			.ExcludeFromDescription();

		Task<CompatStatusDto> StatusAsync(
			HttpContext context,
			ICompatStatusProjector projector,
			CancellationToken cancellationToken)
			=> projector.ProjectAsync(facade, context, cancellationToken);
	}
}

public sealed record CompatApiDiscoveryDto(string Current, IReadOnlyList<string> Deprecated);
