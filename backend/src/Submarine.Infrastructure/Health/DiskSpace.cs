namespace Submarine.Infrastructure.Health;

/// <summary>
///     Queries free and total space of the drive holding a path, null when unavailable.
/// </summary>
public static class DiskSpace
{
	/// <summary>
	///     Free and total bytes of the drive holding the path.
	/// </summary>
	public static (long FreeBytes, long TotalBytes)? Query(string path)
	{
		try
		{
			var root = Path.GetPathRoot(Path.GetFullPath(path));
			if (string.IsNullOrEmpty(root))
			{
				return null;
			}

			var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
					string.Equals(d.Name, root, StringComparison.OrdinalIgnoreCase))
				?? new DriveInfo(root);
			return (drive.AvailableFreeSpace, drive.TotalSize);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return null;
		}
	}
}
