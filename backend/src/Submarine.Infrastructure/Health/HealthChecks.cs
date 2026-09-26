using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Core.Download;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     One health check producing zero or more issues.
/// </summary>
public interface IHealthCheck
{
	/// <summary>
	///     Runs the check.
	/// </summary>
	Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Checks indexer configuration and failure backoff state.
/// </summary>
public sealed class IndexerHealthCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "Indexers";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssueSnapshot>();
		var indexers = await db.Indexers.AsNoTracking()
			.Select(x => new { x.Id, x.Name, x.EnableRss, x.EnableAutomaticSearch })
			.ToListAsync(cancellationToken);
		if (indexers.Count == 0)
		{
			issues.Add(new(HealthIssueType.WARNING, Source, "No indexers are configured", null));
			return issues;
		}

		if (indexers.All(x => !x.EnableRss))
		{
			issues.Add(new(HealthIssueType.WARNING, Source, "No indexers have RSS sync enabled", null));
		}

		if (indexers.All(x => !x.EnableAutomaticSearch))
		{
			issues.Add(new(HealthIssueType.WARNING, Source, "No indexers have automatic search enabled", null));
		}

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var disabledUntil = await db.IndexerStatuses.AsNoTracking()
			.Where(x => x.DisabledUntil > now)
			.Join(db.Indexers, status => status.IndexerId, indexer => indexer.Id,
				(status, indexer) => new { indexer.Name, status.DisabledUntil })
			.ToListAsync(cancellationToken);
		foreach (var status in disabledUntil)
		{
			issues.Add(new(HealthIssueType.WARNING, Source,
				$"Indexer {status.Name} is disabled until {status.DisabledUntil:R} after repeated failures", null));
		}

		return issues;
	}
}

/// <summary>
///     Checks download client configuration and reachability.
/// </summary>
public sealed class DownloadClientHealthCheck(SubmarineDbContext db, IDownloadClientFactory factory) : IHealthCheck
{
	private const string Source = "Download clients";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var clients = await db.DownloadClients.AsNoTracking()
			.Where(x => x.Enable)
			.Select(x => new { x.Id, x.Name, x.Type, x.SettingsJson })
			.ToListAsync(cancellationToken);
		if (clients.Count == 0)
		{
			return [new(HealthIssueType.WARNING, Source, "No download clients are enabled", null)];
		}

		var issues = new List<HealthIssueSnapshot>();

		var failedClients = await db.TrackedDownloads.AsNoTracking()
			.Where(x => x.Status == TrackedDownloadStatus.FAILED || x.Status == TrackedDownloadStatus.WARNING)
			.Select(x => x.DownloadClientId)
			.Distinct()
			.ToListAsync(cancellationToken);
		foreach (var client in clients.Where(x => failedClients.Contains(x.Id)))
		{
			issues.Add(new(HealthIssueType.WARNING, Source, $"{client.Name} has failed downloads waiting for attention", null));
		}

		foreach (var client in clients)
		{
			try
			{
				await factory.Create(client.Type, client.SettingsJson, client.Id, client.Name)
					.TestAsync(cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				issues.Add(new(HealthIssueType.ERROR, Source, $"{client.Name} is unreachable: {ex.Message}", null));
			}
		}

		return issues;
	}
}

/// <summary>
///     Checks root folder existence and free space.
/// </summary>
public sealed class RootFolderHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Root folders";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssueSnapshot>();
		var folders = await db.RootFolders.AsNoTracking().ToListAsync(cancellationToken);
		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		foreach (var folder in folders)
		{
			if (!Directory.Exists(folder.Path))
			{
				issues.Add(new(HealthIssueType.ERROR, Source, $"Root folder {folder.Path} is missing", null));
				continue;
			}

			if (mediaManagement.SkipFreeSpaceCheck)
			{
				continue;
			}

			if (DiskSpace.Query(folder.Path) is { } space
				&& space.FreeBytes < mediaManagement.MinimumFreeSpaceMb * 1024L * 1024L)
			{
				issues.Add(new(HealthIssueType.WARNING, Source,
					$"Root folder {folder.Path} has less than {mediaManagement.MinimumFreeSpaceMb} MB free space left", null));
			}
		}

		return issues;
	}
}

/// <summary>
///     Checks the reachability of the metadata and mappings services.
/// </summary>
public sealed class ServiceHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory) : IHealthCheck
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssueSnapshot>();
		await CheckAsync(issues, "Metadata service", configuration["Metadata:BaseUrl"], cancellationToken);
		await CheckAsync(issues, "Mappings service", configuration["Mappings:BaseUrl"], cancellationToken);
		return issues;
	}

	private async Task CheckAsync(
		List<HealthIssueSnapshot> issues,
		string source,
		string? baseUrl,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(baseUrl))
		{
			return;
		}

		try
		{
			var client = httpClientFactory.CreateClient("health");
			using var response = await client.GetAsync(
				$"{baseUrl.TrimEnd('/')}/_status/healthz",
				cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				issues.Add(new(HealthIssueType.ERROR, source, $"{source} returned {(int)response.StatusCode}", null));
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			issues.Add(new(HealthIssueType.ERROR, source, $"{source} is unreachable: {ex.Message}", null));
		}
	}
}

/// <summary>
///     Checks configuration settings that indicate a broken setup.
/// </summary>
public sealed class SettingsHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssueSnapshot>();
		var indexerConfig = await db.IndexerConfig.AsNoTracking().SingleAsync(cancellationToken);
		if (indexerConfig.RssSyncIntervalMinutes == 0)
		{
			issues.Add(new(HealthIssueType.WARNING, "RSS sync", "RSS sync is disabled", null));
		}

		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		if (!string.IsNullOrEmpty(mediaManagement.RecycleBinPath) && !Directory.Exists(mediaManagement.RecycleBinPath))
		{
			issues.Add(new(HealthIssueType.WARNING, "Recycle bin",
				$"Recycle bin path {mediaManagement.RecycleBinPath} is missing", null));
		}

		var general = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		if (general.AuthMethod == AuthMethod.NONE)
		{
			issues.Add(new(HealthIssueType.NOTICE, "Authentication",
				"Authentication is disabled, anyone with network access can use this instance", null));
		}

		return issues;
	}
}

/// <summary>
///     Reports an available application update.
/// </summary>
public sealed class UpdateHealthCheck(Submarine.Infrastructure.Updates.IUpdateChecker updateChecker) : IHealthCheck
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var info = await updateChecker.GetLatestAsync(cancellationToken: cancellationToken);
		return info.UpdateAvailable
			? [new(HealthIssueType.NOTICE, "Updates", $"New update available: {info.LatestVersion}", info.ReleaseNotesUrl)]
			: [];
	}
}
