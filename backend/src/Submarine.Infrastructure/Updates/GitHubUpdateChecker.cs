using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Submarine.Infrastructure.Updates;

/// <summary>
///     Result of an update check.
/// </summary>
/// <param name="CurrentVersion">Version of the running instance.</param>
/// <param name="LatestVersion">Newest published version, null when unknown.</param>
/// <param name="ReleaseNotesUrl">Url of the newest release notes, null when unknown.</param>
/// <param name="UpdateAvailable">Whether the newest release is newer than the running instance.</param>
/// <param name="CheckFailed">Whether the GitHub release check failed, distinct from a check that succeeded and found no update.</param>
public sealed record UpdateInfo(
	string CurrentVersion,
	string? LatestVersion,
	string? ReleaseNotesUrl,
	bool UpdateAvailable,
	bool CheckFailed);

/// <summary>
///     Checks GitHub for a newer release.
/// </summary>
public interface IUpdateChecker
{
	/// <summary>
	///     Gets the latest release info. Results are cached for six hours.
	/// </summary>
	/// <param name="bypassCache">Force a fresh request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<UpdateInfo> GetLatestAsync(bool bypassCache = false, CancellationToken cancellationToken = default);
}

/// <summary>
///     Queries the GitHub releases API and compares with the assembly informational version.
///     Failures are swallowed and return an unknown latest version.
/// </summary>
public sealed partial class GitHubUpdateChecker(
	IHttpClientFactory httpClientFactory,
	IMemoryCache cache,
	ILogger<GitHubUpdateChecker> logger) : IUpdateChecker
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

	/// <inheritdoc />
	public async Task<UpdateInfo> GetLatestAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
	{
		var current = CurrentVersion();
		if (bypassCache || !cache.TryGetValue(CacheKey, out var cached))
		{
			cached = await FetchAsync(cancellationToken);
			cache.Set(CacheKey, cached, CacheDuration);
		}

		var latestTag = (string?)cached;
		var updateAvailable = latestTag is not null
			&& TryNormalize(latestTag, out var latest)
			&& TryNormalize(current, out var running)
			&& latest > running;
		return new UpdateInfo(
			current,
			latestTag,
			latestTag is null ? null : $"https://github.com/SimplyMedia/Submarine/releases/tag/{latestTag}",
			updateAvailable,
			latestTag is null);
	}

	internal async Task<string?> FetchAsync(CancellationToken cancellationToken)
	{
		var client = httpClientFactory.CreateClient(HttpClientName);
		try
		{
			using var response = await client.GetAsync(ReleaseApiUrl, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				LogFetchFailed(logger, (int)response.StatusCode);
				return null;
			}

			var payload = await response.Content.ReadFromJsonAsync<GithubRelease>(cancellationToken: cancellationToken);
			return payload?.TagName;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogFetchFailed(logger, ex);
			return null;
		}
	}

	internal static bool TryNormalize(string version, out Version normalized)
		=> System.Version.TryParse(version.TrimStart('v', 'V').Split('+', '-')[0], out normalized!);

	internal static string CurrentVersion()
		=> Assembly.GetEntryAssembly()
			?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion
			?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
			?? "0.0.0";

	internal const string CacheKey = "submarine-updates-latest";

	internal const string HttpClientName = "updates";

	internal const string ReleaseApiUrl = "https://api.github.com/repos/SimplyMedia/Submarine/releases/latest";

	[LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed with status {Status}")]
	private static partial void LogFetchFailed(ILogger logger, int status);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed")]
	private static partial void LogFetchFailed(ILogger logger, Exception exception);

	internal sealed class GithubRelease
	{
		[JsonPropertyName("tag_name")]
		public string? TagName { get; set; }
	}
}
