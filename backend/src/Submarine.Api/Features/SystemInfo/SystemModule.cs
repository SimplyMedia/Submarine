using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Api.Modules;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.SystemInfo;

/// <summary>
///     Health probes and instance status.
/// </summary>
public sealed class SystemModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/_status/healthz", () => TypedResults.Text("Healthy"))
			.Produces<string>(StatusCodes.Status200OK, contentType: "text/plain")
			.AllowAnonymous();
		endpoints.MapGet("/_status/ready", ReadyAsync)
			.Produces<string>(StatusCodes.Status200OK, contentType: "text/plain")
			.AllowAnonymous();
		endpoints.MapGet("/api/v1/system/status", StatusAsync);
	}

	private static async Task<ContentHttpResult> ReadyAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> await db.Database.CanConnectAsync(cancellationToken)
			? TypedResults.Text("Ready")
			: TypedResults.Text("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);

	private static async Task<Ok<SystemStatusDto>> StatusAsync(
		SubmarineDbContext db,
		IAuthConfigProvider authConfigProvider,
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var general = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		var snapshot = await authConfigProvider.GetSnapshotAsync(cancellationToken);
		var version = Assembly.GetEntryAssembly()
			?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion
			?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
			?? "unknown";
		var process = Process.GetCurrentProcess();
		return TypedResults.Ok(new SystemStatusDto(
			Version: version,
			Os: RuntimeInformation.OSDescription,
			Runtime: RuntimeInformation.FrameworkDescription,
			StartTime: DateTime.SpecifyKind(process.StartTime, DateTimeKind.Local).ToUniversalTime(),
			AppData: Environment.CurrentDirectory,
			IsDocker: File.Exists("/.dockerenv"),
			DatabaseProvider: SubmarineDatabase.Provider(configuration),
			UrlBase: general.UrlBase,
			AuthMethod: snapshot.Method.ToString(),
			InstanceName: general.InstanceName,
			MetadataUrl: configuration["Metadata:BaseUrl"] ?? string.Empty,
			MappingsUrl: configuration["Mappings:BaseUrl"] ?? string.Empty));
	}
}

/// <summary>System status payload.</summary>
/// <param name="Version">Application version.</param>
/// <param name="Os">Operating system description.</param>
/// <param name="Runtime">Runtime description.</param>
/// <param name="StartTime">UTC process start time.</param>
/// <param name="AppData">Working data directory.</param>
/// <param name="IsDocker">Whether the app runs inside Docker.</param>
/// <param name="DatabaseProvider">Configured database provider.</param>
/// <param name="UrlBase">Configured base URL.</param>
/// <param name="AuthMethod">Current auth method.</param>
/// <param name="InstanceName">Instance display name.</param>
/// <param name="MetadataUrl">Base URL of the metadata service.</param>
/// <param name="MappingsUrl">Base URL of the mappings service.</param>
public sealed record SystemStatusDto(
	string Version,
	string Os,
	string Runtime,
	DateTime StartTime,
	string AppData,
	bool IsDocker,
	string DatabaseProvider,
	string UrlBase,
	string AuthMethod,
	string InstanceName,
	string MetadataUrl,
	string MappingsUrl);
