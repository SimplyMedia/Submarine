using System.Runtime.InteropServices;
using Submarine.Core.Config;

namespace Submarine.Api.Services;

/// <summary>
///     Places a media file at a destination, preferring a hardlink and falling back to a move
/// </summary>
public static class FileLinker
{
	/// <summary>
	///     Places <paramref name="source" /> at <paramref name="destination" />, creating parent directories
	/// </summary>
	/// <param name="source">The existing file</param>
	/// <param name="destination">The destination path</param>
	/// <param name="useHardlink">Whether to attempt a hardlink before moving</param>
	/// <param name="minimumFreeSpaceMb">
	///     Minimum free space in megabytes required on the destination drive, 0 skips the check
	/// </param>
	/// <param name="config">Media management config supplying Unix chmod/chown to apply after placement, if any</param>
	/// <param name="logger">Logger used when applying Unix permissions</param>
	/// <exception cref="InvalidOperationException">The destination drive has less free space than required</exception>
	/// <remarks>
	///     An existing destination is overwritten. The content stays safe because <paramref name="source" /> holds
	///     it until the hardlink or move completes.
	/// </remarks>
	public static void Place(string source, string destination, bool useHardlink, int minimumFreeSpaceMb = 0,
		MediaManagementConfig? config = null, ILogger? logger = null)
	{
		var directory = Path.GetDirectoryName(destination);

		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);

			if (config != null && logger != null)
				PermissionApplier.ApplyToDirectory(directory, config, logger);
		}

		if (minimumFreeSpaceMb > 0)
			EnsureFreeSpace(destination, minimumFreeSpaceMb);

		if (File.Exists(destination))
			File.Delete(destination);

		if (!useHardlink || !TryHardLink(source, destination))
			File.Move(source, destination, false);

		if (config != null && logger != null)
			PermissionApplier.ApplyToFile(destination, config, logger);
	}

	private static void EnsureFreeSpace(string destination, int minimumFreeSpaceMb)
	{
		var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!);

		if (drive.AvailableFreeSpace >= minimumFreeSpaceMb * 1024L * 1024L)
			return;

		throw new InvalidOperationException($"insufficient free space on {drive.Name}");
	}

	private static bool TryHardLink(string source, string destination)
	{
		try
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? CreateHardLinkW(destination, source, IntPtr.Zero)
				: link(source, destination) == 0;
		}
		catch
		{
			return false;
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateHardLinkW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);

	[DllImport("libc", SetLastError = true, EntryPoint = "link")]
	private static extern int link(string oldPath, string newPath);
}
