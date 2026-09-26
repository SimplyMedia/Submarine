namespace Submarine.Infrastructure.Import;

/// <summary>
///     Moves replaced files to the recycle bin, mirroring their path relative to the root folder they came
///     from. An empty recycle bin path means files are deleted immediately.
/// </summary>
public interface IRecycleBinService
{
	/// <summary>
	///     Recycle a file. Deletes it immediately when <paramref name="recycleBinPath" /> is empty.
	/// </summary>
	void Recycle(string filePath, string rootFolderPath, string recycleBinPath, TimeProvider timeProvider);

	/// <summary>
	///     Deletes recycle bin entries whose last write time is older than the cutoff, then prunes empty folders.
	///     Returns the number of files deleted.
	/// </summary>
	int CleanupOlderThan(string recycleBinPath, DateTime cutoffUtc);
}

/// <inheritdoc cref="IRecycleBinService" />
public sealed class RecycleBinService : IRecycleBinService
{
	/// <inheritdoc />
	public void Recycle(string filePath, string rootFolderPath, string recycleBinPath, TimeProvider timeProvider)
	{
		if (!File.Exists(filePath))
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(recycleBinPath))
		{
			File.Delete(filePath);
			return;
		}

		var relative = Path.GetRelativePath(rootFolderPath, filePath);
		var destination = Path.Combine(recycleBinPath, relative);
		var destinationDirectory = Path.GetDirectoryName(destination);
		if (!string.IsNullOrEmpty(destinationDirectory))
		{
			Directory.CreateDirectory(destinationDirectory);
		}

		if (File.Exists(destination))
		{
			var stamp = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmss");
			destination = Path.Combine(
				Path.GetDirectoryName(destination) ?? recycleBinPath,
				$"{Path.GetFileNameWithoutExtension(destination)}_{stamp}{Path.GetExtension(destination)}");
		}

		File.Move(filePath, destination);
		// Cleanup ages files by write time; a move keeps the original, so stamp the recycle time.
		File.SetLastWriteTimeUtc(destination, timeProvider.GetUtcNow().UtcDateTime);
	}

	/// <inheritdoc />
	public int CleanupOlderThan(string recycleBinPath, DateTime cutoffUtc)
	{
		if (string.IsNullOrWhiteSpace(recycleBinPath) || !Directory.Exists(recycleBinPath))
		{
			return 0;
		}

		var deleted = 0;
		foreach (var file in Directory.EnumerateFiles(recycleBinPath, "*", SearchOption.AllDirectories))
		{
			if (File.GetLastWriteTimeUtc(file) >= cutoffUtc)
			{
				continue;
			}

			File.Delete(file);
			deleted++;
		}

		foreach (var directory in Directory.EnumerateDirectories(recycleBinPath, "*", SearchOption.AllDirectories)
			.OrderByDescending(x => x.Length))
		{
			if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
			{
				Directory.Delete(directory);
			}
		}

		return deleted;
	}
}
