namespace Submarine.Core.MediaFiles;

/// <summary>
///     Validates a media version's folder path and resolves it under its root folder, refusing anything
///     that would escape the root (absolute paths, "..", or a rendered name that contains a separator).
/// </summary>
public static class MediaVersionPathGuard
{
	/// <summary>
	///     True when <paramref name="path" /> is safe to store as <c>MediaVersion.Path</c>: a single,
	///     non-empty relative segment with no separators, and not "." or "..".
	/// </summary>
	public static bool IsSingleRelativeSegment(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		if (path.Contains('/') || path.Contains('\\') || Path.IsPathRooted(path))
		{
			return false;
		}

		return path != "." && path != "..";
	}

	/// <summary>
	///     Combines <paramref name="rootPath" /> with <paramref name="relativePath" /> and any
	///     <paramref name="extraSegments" />, throwing <see cref="InvalidOperationException" /> when the
	///     canonical result does not stay strictly under the root. Callers must validate the outcome before
	///     any filesystem write, delete or move.
	/// </summary>
	public static string ResolveUnderRoot(string rootPath, string relativePath, params string[] extraSegments)
	{
		var rootFull = Path.GetFullPath(rootPath);
		var combined = Path.Combine([rootPath, relativePath, .. extraSegments]);
		var fullPath = Path.GetFullPath(combined);
		var rootWithSeparator = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

		if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
		{
			throw new InvalidOperationException($"Resolved path '{fullPath}' escapes root folder '{rootFull}'");
		}

		return fullPath;
	}
}
