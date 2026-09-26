using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     Places an imported file at its destination: hardlink when requested and possible, otherwise move,
///     falling back to copy and delete across volumes. Applies configured Unix permissions.
/// </summary>
public interface IFileLinker
{
	/// <summary>
	///     Link or move a file into place, creating destination folders as needed. Returns true when the
	///     source file was hardlinked (so the source still exists and must not be treated as consumed).
	/// </summary>
	bool LinkOrMove(string sourcePath, string destinationPath, bool useHardlinks);

	/// <summary>
	///     Apply configured chmod (octal string, e.g. "755") to a folder or file on Unix. No-op on Windows or
	///     when the mode string is empty.
	/// </summary>
	void ApplyPermissions(string path, string? chmod, bool isDirectory);
}

/// <inheritdoc cref="IFileLinker" />
public sealed class FileLinker(ILogger<FileLinker> logger) : IFileLinker
{
	private static readonly StringComparison PathComparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

	/// <inheritdoc />
	public bool LinkOrMove(string sourcePath, string destinationPath, bool useHardlinks)
	{
		if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), PathComparison))
		{
			// Already in place: nothing to link or move, and never delete the file we were asked to place.
			return true;
		}

		var destinationDirectory = Path.GetDirectoryName(destinationPath);
		if (!string.IsNullOrEmpty(destinationDirectory))
		{
			Directory.CreateDirectory(destinationDirectory);
		}

		if (File.Exists(destinationPath))
		{
			File.Delete(destinationPath);
		}

		if (useHardlinks && TryHardLink(sourcePath, destinationPath))
		{
			return true;
		}

		try
		{
			File.Move(sourcePath, destinationPath, overwrite: true);
		}
		catch (IOException)
		{
			File.Copy(sourcePath, destinationPath, overwrite: true);
			File.Delete(sourcePath);
		}

		return false;
	}

	/// <inheritdoc />
	public void ApplyPermissions(string path, string? chmod, bool isDirectory)
	{
		if (string.IsNullOrWhiteSpace(chmod) || !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
		{
			return;
		}

		try
		{
			var mode = Convert.ToInt32(chmod, 8);
			File.SetUnixFileMode(path, (UnixFileMode)mode);
		}
		catch (Exception exception) when (exception is FormatException or OverflowException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(exception, "Failed to apply permissions {Mode} to {Path}", chmod, path);
		}
	}

	private bool TryHardLink(string sourcePath, string destinationPath)
	{
		try
		{
			var result = OperatingSystem.IsWindows()
				? CreateHardLinkWindows(destinationPath, sourcePath, IntPtr.Zero)
				: LinkUnix(sourcePath, destinationPath) == 0;

			if (!result)
			{
				logger.LogDebug("Hardlink from {Source} to {Destination} not possible, falling back to move", sourcePath, destinationPath);
			}

			return result;
		}
		catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
		{
			return false;
		}
	}

	[DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CreateHardLinkWindows(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

	[DllImport("libc", EntryPoint = "link", SetLastError = true)]
	private static extern int LinkUnix(string oldPath, string newPath);
}
