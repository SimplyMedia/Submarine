using Submarine.Core.Enums;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

public interface ICompatStatusProjector
{
	Task<CompatStatusDto> ProjectAsync(string facade, HttpContext context, CancellationToken cancellationToken);
}

public sealed class CompatStatusProjector(
	SubmarineDbContext db,
	IAuthConfigProvider authConfigProvider,
	IConfiguration configuration,
	IHostEnvironment environment) : ICompatStatusProjector
{
	public async Task<CompatStatusDto> ProjectAsync(string facade, HttpContext context, CancellationToken cancellationToken)
	{
		var general = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		var auth = await authConfigProvider.GetSnapshotAsync(cancellationToken);
		var process = Process.GetCurrentProcess();
		var contractVersion = facade switch
		{
			"sonarr" => "4.0.20.3014",
			"radarr" => "6.4.4.10685",
			"prowlarr" => "2.6.5.5623",
			_ => throw new ArgumentOutOfRangeException(nameof(facade))
		};
		var actualVersion = Assembly.GetEntryAssembly()
			?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
			?? "unknown";
		var runtimeVersion = Environment.Version.ToString();
		var runtimeName = RuntimeInformation.FrameworkDescription;
#if DEBUG
		const bool isDebug = true;
#else
		const bool isDebug = false;
#endif
		return new CompatStatusDto(
			AppName: facade switch { "sonarr" => "Sonarr", "radarr" => "Radarr", _ => "Prowlarr" },
			InstanceName: general.InstanceName,
			Version: contractVersion,
			SubmarineVersion: actualVersion,
			UrlBase: context.Request.PathBase.Value ?? auth.UrlBase,
			StartTime: DateTime.SpecifyKind(process.StartTime, DateTimeKind.Local).ToUniversalTime(),
			IsDocker: File.Exists("/.dockerenv") || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true",
			IsLinux: OperatingSystem.IsLinux(),
			IsOsx: OperatingSystem.IsMacOS(),
			IsWindows: OperatingSystem.IsWindows(),
			IsMonoRuntime: false,
			IsMono: false,
			IsDebug: isDebug,
			IsProduction: environment.IsProduction(),
			RuntimeName: runtimeName,
			RuntimeVersion: runtimeVersion,
			Branch: general.Branch,
			Authentication: auth.Method switch
			{
				AuthMethod.NONE => "none",
				AuthMethod.FORMS => "forms",
				AuthMethod.BASIC => "basic",
				AuthMethod.EXTERNAL => "external",
				_ => throw new ArgumentOutOfRangeException(nameof(auth.Method))
			},
			DatabaseType: SubmarineDatabase.Provider(configuration));
	}
}

public sealed record CompatStatusDto(
	string AppName,
	string InstanceName,
	string Version,
	string SubmarineVersion,
	string UrlBase,
	DateTime StartTime,
	bool IsDocker,
	bool IsLinux,
	bool IsOsx,
	bool IsWindows,
	bool IsMonoRuntime,
	bool IsMono,
	bool IsDebug,
	bool IsProduction,
	string RuntimeName,
	string RuntimeVersion,
	string Branch,
	string Authentication,
	string DatabaseType);
