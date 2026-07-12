namespace Submarine.Api.Services;

/// <summary>
///     Handles subtitle sidecar files accompanying a video file
/// </summary>
public static class ExtraFileService
{
	private static readonly string[] SubtitleExtensions = { ".srt", ".ass", ".ssa", ".sub", ".vtt" };

	/// <summary>
	///     Copies subtitle sidecars of <paramref name="sourceVideo" /> next to <paramref name="destinationVideo" />,
	///     swapping the filename stem and preserving language/forced suffixes (e.g. ".en.forced.srt")
	/// </summary>
	/// <param name="sourceVideo">Path the video file was imported from</param>
	/// <param name="destinationVideo">Path the video file was placed at</param>
	/// <remarks>Sidecars are copied, not moved; source cleanup belongs to the download client</remarks>
	public static void CopySubtitles(string sourceVideo, string destinationVideo)
		=> TransferSubtitles(sourceVideo, destinationVideo, (source, target) => File.Copy(source, target, true));

	/// <summary>
	///     Moves subtitle sidecars of <paramref name="currentVideo" /> alongside <paramref name="newVideo" />,
	///     swapping the filename stem and preserving language/forced suffixes
	/// </summary>
	/// <param name="currentVideo">Path of the video file before the rename</param>
	/// <param name="newVideo">Path of the video file after the rename</param>
	public static void RenameSubtitles(string currentVideo, string newVideo)
		=> TransferSubtitles(currentVideo, newVideo, (source, target) => File.Move(source, target, true));

	private static void TransferSubtitles(string video, string targetVideo, Action<string, string> transfer)
	{
		var directory = Path.GetDirectoryName(video);

		if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
			return;

		var stem = Path.GetFileNameWithoutExtension(video);
		var targetDirectory = Path.GetDirectoryName(targetVideo) ?? "";
		var targetStem = Path.GetFileNameWithoutExtension(targetVideo);

		foreach (var file in Directory.EnumerateFiles(directory))
		{
			var suffix = MatchSidecarSuffix(Path.GetFileName(file), stem);

			if (suffix == null)
				continue;

			transfer(file, Path.Combine(targetDirectory, targetStem + suffix));
		}
	}

	private static string? MatchSidecarSuffix(string fileName, string stem)
	{
		if (fileName.Length <= stem.Length || !fileName.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
			return null;

		var suffix = fileName[stem.Length..];

		if (!suffix.StartsWith('.'))
			return null;

		return SubtitleExtensions.Contains(Path.GetExtension(suffix).ToLowerInvariant()) ? suffix : null;
	}
}
