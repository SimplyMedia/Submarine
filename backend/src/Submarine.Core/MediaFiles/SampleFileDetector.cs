namespace Submarine.Core.MediaFiles;

/// <summary>
///     Detects sample files and in-progress download artifacts that must not be imported.
/// </summary>
public static class SampleFileDetector
{
	private const long SampleMaxBytes = 150L * 1024 * 1024;

	private static readonly string[] IgnoredExtensions = [".!qb", ".part"];

	/// <summary>
	///     Whether the file is a sample: smaller than 150 MB, "sample" in the name, and other candidate media
	///     files exist alongside it.
	/// </summary>
	public static bool IsSample(string fileName, long size, bool otherMediaFilesExist)
		=> otherMediaFilesExist
		   && size < SampleMaxBytes
		   && fileName.Contains("sample", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	///     Whether the file is an in-progress download artifact (qBittorrent incomplete or generic .part file)
	///     that must never be imported.
	/// </summary>
	public static bool IsIncompleteDownloadArtifact(string fileName)
		=> IgnoredExtensions.Any(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
}
