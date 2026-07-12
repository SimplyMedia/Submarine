using System.Runtime.InteropServices;

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
	/// <remarks>
	///     An existing destination is overwritten. The content stays safe because <paramref name="source" /> holds
	///     it until the hardlink or move completes.
	/// </remarks>
	public static void Place(string source, string destination, bool useHardlink)
	{
		var directory = Path.GetDirectoryName(destination);

		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		if (File.Exists(destination))
			File.Delete(destination);

		if (useHardlink && TryHardLink(source, destination))
			return;

		File.Move(source, destination, false);
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
