using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Submarine.Api.Auth;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Shared route-group construction for compatibility facades.</summary>
public static class CompatRoutes
{
	public const string AuthorizationPolicy = "CompatApiKey";

	public static RouteGroupBuilder CreateFacadeGroup(IEndpointRouteBuilder endpoints, string facade)
		=> endpoints.MapGroup($"/compat/{facade}")
			.RequireAuthorization(AuthorizationPolicy)
			.ExcludeFromDescription();

	public static RouteGroupBuilder CreateRestGroup(IEndpointRouteBuilder endpoints, string facade, string apiVersion)
		=> CreateFacadeGroup(endpoints, facade).MapGroup($"/api/{apiVersion}")
			.WithMetadata(new AuthorizeAttribute(AuthorizationPolicy));
}
