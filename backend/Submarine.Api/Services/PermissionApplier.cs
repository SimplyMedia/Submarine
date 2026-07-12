using System.Diagnostics;
using Submarine.Core.Config;

namespace Submarine.Api.Services;

/// <summary>
///     Applies the configured Unix file mode and ownership to placed files and folders. A no-op on Windows.
/// </summary>
public static class PermissionApplier
{
	private static readonly HashSet<string> WarnedInvalidModes = new();

	/// <summary>
	///     Applies <see cref="MediaManagementConfig.ChmodFile" /> and the configured ownership to a file
	/// </summary>
	public static void ApplyToFile(string path, MediaManagementConfig config, ILogger logger)
	{
		if (!IsUnix())
			return;

		ApplyMode(path, config.ChmodFile, logger);
		ApplyOwner(path, config, logger);
	}

	/// <summary>
	///     Applies <see cref="MediaManagementConfig.ChmodFolder" /> and the configured ownership to a directory
	/// </summary>
	public static void ApplyToDirectory(string path, MediaManagementConfig config, ILogger logger)
	{
		if (!IsUnix())
			return;

		ApplyMode(path, config.ChmodFolder, logger);
		ApplyOwner(path, config, logger);
	}

	/// <summary>
	///     Parses an octal file mode (e.g. "755") into a <see cref="UnixFileMode" />
	/// </summary>
	/// <returns>True when <paramref name="octal" /> is a valid octal mode</returns>
	public static bool TryParseMode(string? octal, out UnixFileMode mode)
	{
		mode = default;

		if (string.IsNullOrWhiteSpace(octal))
			return false;

		try
		{
			mode = (UnixFileMode)Convert.ToInt32(octal, 8);
			return true;
		}
		catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
		{
			return false;
		}
	}

	private static bool IsUnix()
		=> OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

	private static void ApplyMode(string path, string? octal, ILogger logger)
	{
		if (string.IsNullOrEmpty(octal))
			return;

		if (!TryParseMode(octal, out var mode))
		{
			WarnInvalidMode(octal, logger);
			return;
		}

		File.SetUnixFileMode(path, mode);
	}

	private static void WarnInvalidMode(string octal, ILogger logger)
	{
		lock (WarnedInvalidModes)
			if (!WarnedInvalidModes.Add(octal))
				return;

		logger.LogWarning("Invalid octal file mode '{Mode}', skipping chmod", octal);
	}

	private static void ApplyOwner(string path, MediaManagementConfig config, ILogger logger)
	{
		if (string.IsNullOrEmpty(config.ChownUser))
			return;

		var spec = string.IsNullOrEmpty(config.ChownGroup)
			? $"{config.ChownUser}:"
			: $"{config.ChownUser}:{config.ChownGroup}";

		try
		{
			var info = new ProcessStartInfo("chown") { UseShellExecute = false };
			info.ArgumentList.Add(spec);
			info.ArgumentList.Add(path);

			using var process = Process.Start(info);

			if (process == null)
				return;

			process.WaitForExit();

			if (process.ExitCode != 0)
				logger.LogWarning("chown {Spec} {Path} exited with code {ExitCode}", spec, path, process.ExitCode);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "chown {Spec} {Path} failed", spec, path);
		}
	}
}
