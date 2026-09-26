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
/// <param name="LatestVersion">Newest published version for the configured branch, null when unknown.</param>
/// <param name="ReleaseNotesUrl">Url of the newest release notes, null when unknown.</param>
/// <param name="UpdateAvailable">Whether the newest release is newer than the running instance.</param>
/// <param name="CheckFailed">Whether the GitHub release check failed, distinct from a check that succeeded and found no matching release.</param>
public sealed record UpdateInfo(
	string CurrentVersion,
	string? LatestVersion,
	string? ReleaseNotesUrl,
	bool UpdateAvailable,
	bool CheckFailed);

/// <summary>
///     One published release, for the System &gt; Updates history view.
/// </summary>
/// <param name="Version">The release tag.</param>
/// <param name="Notes">The release notes body, in GitHub flavoured markdown.</param>
/// <param name="HtmlUrl">Url of the release page.</param>
/// <param name="Prerelease">Whether this is a develop prerelease rather than a stable release.</param>
/// <param name="PublishedAt">When the release was published.</param>
/// <param name="Installed">Whether this release matches the running version.</param>
public sealed record ReleaseInfo(
	string Version,
	string Notes,
	string HtmlUrl,
	bool Prerelease,
	DateTimeOffset PublishedAt,
	bool Installed);

/// <summary>
///     Checks GitHub for a newer release.
/// </summary>
public interface IUpdateChecker
{
	/// <summary>
	///     Gets the latest release info for the given update branch: "develop" selects the newest
	///     prerelease (matching the images the build workflow tags "develop"), any other branch
	///     selects the newest stable release (matching the images tagged "latest" from master).
	///     Results are cached for six hours.
	/// </summary>
	/// <param name="branch">The configured update branch, e.g. "master" or "develop".</param>
	/// <param name="bypassCache">Force a fresh request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<UpdateInfo> GetLatestAsync(string branch, bool bypassCache = false, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets the most recent published releases (stable and prerelease), newest first, for the
	///     System &gt; Updates history view. Results are cached for six hours.
	/// </summary>
	/// <param name="bypassCache">Force a fresh request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(bool bypassCache = false, CancellationToken cancellationToken = default);
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
	private const int ReleaseHistoryCount = 10;

	/// <inheritdoc />
	public async Task<UpdateInfo> GetLatestAsync(string branch, bool bypassCache = false, CancellationToken cancellationToken = default)
	{
		var current = CurrentVersion();
		var releases = await FetchReleasesAsync(bypassCache, cancellationToken);
		if (releases is null)
		{
			return new UpdateInfo(current, null, null, false, CheckFailed: true);
		}

		var candidate = SelectLatest(releases, branch);
		var updateAvailable = candidate is not null
			&& TryNormalize(candidate.TagName!, out var latest)
			&& TryNormalize(current, out var running)
			&& latest > running;
		return new UpdateInfo(
			current,
			candidate?.TagName,
			candidate is null ? null : $"https://github.com/SimplyMedia/Submarine/releases/tag/{candidate.TagName}",
			updateAvailable,
			CheckFailed: false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
	{
		var releases = await FetchReleasesAsync(bypassCache, cancellationToken);
		if (releases is null)
		{
			return [];
		}

		var current = CurrentVersion();
		var currentNormalized = TryNormalize(current, out var currentVersion) ? currentVersion : null;
		return releases
			.Where(release => release.TagName is not null)
			.Select(release => new ReleaseInfo(
				release.TagName!,
				release.Body ?? string.Empty,
				$"https://github.com/SimplyMedia/Submarine/releases/tag/{release.TagName}",
				release.Prerelease,
				release.PublishedAt,
				currentNormalized is not null && TryNormalize(release.TagName!, out var releaseVersion) && releaseVersion == currentNormalized))
			.ToList();
	}

	/// <summary>
	///     Newest release matching the branch's channel: prereleases for "develop", stable
	///     releases otherwise. GitHub's releases list is already sorted newest first.
	/// </summary>
	internal static GithubRelease? SelectLatest(IReadOnlyList<GithubRelease> releases, string branch)
	{
		var wantsPrerelease = string.Equals(branch, "develop", StringComparison.OrdinalIgnoreCase);
		return releases.FirstOrDefault(release => release.Prerelease == wantsPrerelease);
	}

	internal async Task<IReadOnlyList<GithubRelease>?> FetchReleasesAsync(bool bypassCache, CancellationToken cancellationToken)
	{
		if (!bypassCache && cache.TryGetValue(CacheKey, out IReadOnlyList<GithubRelease>? cached) && cached is not null)
		{
			return cached;
		}

		var client = httpClientFactory.CreateClient(HttpClientName);
		try
		{
			using var response = await client.GetAsync($"{ReleasesApiUrl}?per_page={ReleaseHistoryCount}", cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				LogFetchFailed(logger, (int)response.StatusCode);
				return null;
			}

			var payload = await response.Content.ReadFromJsonAsync<List<GithubRelease>>(cancellationToken: cancellationToken);
			if (payload is null)
			{
				return null;
			}

			IReadOnlyList<GithubRelease> releases = payload;
			cache.Set(CacheKey, releases, CacheDuration);
			return releases;
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

	internal const string CacheKey = "submarine-updates-releases";

	internal const string HttpClientName = "updates";

	internal const string ReleasesApiUrl = "https://api.github.com/repos/SimplyMedia/Submarine/releases";

	[LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed with status {Status}")]
	private static partial void LogFetchFailed(ILogger logger, int status);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed")]
	private static partial void LogFetchFailed(ILogger logger, Exception exception);

	internal sealed class GithubRelease
	{
		[JsonPropertyName("tag_name")]
		public string? TagName { get; set; }

		[JsonPropertyName("prerelease")]
		public bool Prerelease { get; set; }

		[JsonPropertyName("body")]
		public string? Body { get; set; }

		[JsonPropertyName("published_at")]
		public DateTimeOffset PublishedAt { get; set; }
	}
}
