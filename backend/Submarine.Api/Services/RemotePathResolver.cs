using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Core.Download;

namespace Submarine.Api.Services;

/// <summary>
///     Resolves paths reported by a remote Download Client to local paths via configured Remote Path Mappings
/// </summary>
public class RemotePathResolver
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly SubmarineDatabaseContext _context;

	public RemotePathResolver(SubmarineDatabaseContext context)
		=> _context = context;

	/// <summary>
	///     Loads all configured Remote Path Mappings
	/// </summary>
	public Task<List<RemotePathMapping>> GetMappingsAsync(CancellationToken cancellationToken = default)
		=> _context.RemotePathMappings.AsNoTracking().ToListAsync(cancellationToken);

	/// <summary>
	///     Extracts the "Host" property from a Download Client's settings JSON, if present
	/// </summary>
	public static string? ExtractHost(string? settingsJson)
	{
		if (string.IsNullOrEmpty(settingsJson))
			return null;

		try
		{
			return JsonSerializer.Deserialize<HostSettings>(settingsJson, JsonOptions)?.Host;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>
	///     Replaces the longest matching mapping's RemotePath prefix on <paramref name="remotePath" /> with its
	///     LocalPath, scoped to mappings for <paramref name="clientHost" />. Returns <paramref name="remotePath" />
	///     unchanged when no mapping matches
	/// </summary>
	public static string Resolve(string? clientHost, string remotePath, IReadOnlyList<RemotePathMapping> mappings)
	{
		if (string.IsNullOrEmpty(clientHost) || string.IsNullOrEmpty(remotePath))
			return remotePath;

		var normalizedRemotePath = Normalize(remotePath);

		var match = mappings
			.Where(m => string.Equals(m.Host, clientHost, StringComparison.OrdinalIgnoreCase))
			.Select(m => (Mapping: m, NormalizedPrefix: Normalize(m.RemotePath)))
			.Where(m => normalizedRemotePath.StartsWith(m.NormalizedPrefix, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(m => m.NormalizedPrefix.Length)
			.FirstOrDefault();

		if (match.Mapping == null)
			return remotePath;

		var localSeparator = match.Mapping.LocalPath.Contains('\\') ? '\\' : '/';
		var suffix = normalizedRemotePath[match.NormalizedPrefix.Length..]
			.Replace('/', localSeparator)
			.TrimStart(localSeparator);

		var localPath = match.Mapping.LocalPath.TrimEnd('\\', '/');

		return suffix.Length == 0 ? localPath : $"{localPath}{localSeparator}{suffix}";
	}

	/// <summary>
	///     Converts backslashes to forward slashes and trims a trailing separator, so prefix matching is not sensitive
	///     to the separator style of the configured mapping vs. the incoming path
	/// </summary>
	private static string Normalize(string path)
		=> path.Replace('\\', '/').TrimEnd('/');

	private sealed record HostSettings(string? Host);
}
