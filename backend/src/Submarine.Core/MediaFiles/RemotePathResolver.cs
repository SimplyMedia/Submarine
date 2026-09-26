using Submarine.Core.Entities;

namespace Submarine.Core.MediaFiles;

/// <summary>
///     Resolves a download client's remote path to a local path using the configured remote path mappings.
///     Matching is by host (case-insensitive) and the longest matching remote path prefix, separator-safe.
/// </summary>
public static class RemotePathResolver
{
	/// <summary>
	///     Translate a remote path reported by a download client into a local path. Returns the original path
	///     unchanged when no mapping matches.
	/// </summary>
	/// <param name="mappings">Configured remote path mappings.</param>
	/// <param name="host">Host the download client reported, or null when unknown.</param>
	/// <param name="remotePath">Path as reported by the download client.</param>
	public static string ToLocal(IReadOnlyCollection<RemotePathMapping> mappings, string? host, string remotePath)
	{
		if (string.IsNullOrEmpty(remotePath) || host is null)
		{
			return remotePath;
		}

		var match = mappings
			.Where(mapping => string.Equals(mapping.Host, host, StringComparison.OrdinalIgnoreCase))
			.Where(mapping => IsPrefixMatch(remotePath, mapping.RemotePath))
			.OrderByDescending(mapping => mapping.RemotePath.Length)
			.FirstOrDefault();

		if (match is null)
		{
			return remotePath;
		}

		var normalizedPath = remotePath.Replace('\\', '/');
		var normalizedPrefix = match.RemotePath.Replace('\\', '/').TrimEnd('/');
		var suffix = normalizedPath[normalizedPrefix.Length..].TrimStart('/');

		var localBase = match.LocalPath.TrimEnd('/', '\\');
		return suffix.Length == 0 ? localBase : $"{localBase}/{suffix}";
	}

	private static bool IsPrefixMatch(string path, string prefix)
	{
		var normalizedPath = path.Replace('\\', '/');
		var normalizedPrefix = prefix.Replace('\\', '/').TrimEnd('/');

		if (normalizedPrefix.Length == 0)
		{
			return false;
		}

		if (!normalizedPath.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return normalizedPath.Length == normalizedPrefix.Length || normalizedPath[normalizedPrefix.Length] == '/';
	}
}
