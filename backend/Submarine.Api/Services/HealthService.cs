using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Response;
using Submarine.Core.Provider;

namespace Submarine.Api.Services;

/// <summary>
///     Checks the system configuration and state for issues worth surfacing to the user
/// </summary>
public class HealthService
{
	private readonly SubmarineDatabaseContext _context;
	private readonly SettingsService _settings;
	private readonly IMetadataClient _metadataClient;
	private readonly IMappingsClient _mappingsClient;
	private readonly ILogger<HealthService> _logger;

	public HealthService(SubmarineDatabaseContext context, SettingsService settings, IMetadataClient metadataClient,
		IMappingsClient mappingsClient, ILogger<HealthService> logger)
	{
		_context = context;
		_settings = settings;
		_metadataClient = metadataClient;
		_mappingsClient = mappingsClient;
		_logger = logger;
	}

	public async Task<IReadOnlyList<HealthIssue>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssue>();

		await RunCheckAsync(issues, "indexers", () => CheckIndexersAsync(cancellationToken));
		await RunCheckAsync(issues, "downloadClients", () => CheckDownloadClientsAsync(cancellationToken));
		await RunCheckAsync(issues, "rootFolders", () => CheckRootFoldersAsync(cancellationToken));
		await RunCheckAsync(issues, "metadata", () => CheckMetadataAsync(cancellationToken));
		await RunCheckAsync(issues, "mappings", () => CheckMappingsAsync(cancellationToken));
		await RunCheckAsync(issues, "rss", CheckRssAsync);

		return issues;
	}

	// a check that throws is itself a health problem, surfaced as a warning instead of failing the whole endpoint
	private async Task RunCheckAsync(List<HealthIssue> issues, string source,
		Func<Task<IEnumerable<HealthIssue>>> check)
	{
		try
		{
			issues.AddRange(await check());
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Health check {Source} failed", source);
			issues.Add(new HealthIssue("warning", source, $"Health check failed: {ex.Message}"));
		}
	}

	private async Task<IEnumerable<HealthIssue>> CheckIndexersAsync(CancellationToken cancellationToken)
	{
		var hasEnabled = await _context.Providers.AsNoTracking()
			.Where(p => p is TorznabIndexer || p is NewznabIndexer)
			.AnyAsync(p => p.Mode != ProviderMode.NONE, cancellationToken);

		return hasEnabled
			? Array.Empty<HealthIssue>()
			: [new HealthIssue("warning", "indexers", "No indexers are enabled")];
	}

	private async Task<IEnumerable<HealthIssue>> CheckDownloadClientsAsync(CancellationToken cancellationToken)
	{
		var hasEnabled = await _context.DownloadClients.AsNoTracking().AnyAsync(c => c.Enable, cancellationToken);

		return hasEnabled
			? Array.Empty<HealthIssue>()
			: [new HealthIssue("warning", "downloadClients", "No download clients are enabled")];
	}

	private async Task<IEnumerable<HealthIssue>> CheckRootFoldersAsync(CancellationToken cancellationToken)
	{
		var rootFolders = await _context.RootFolders.AsNoTracking().ToListAsync(cancellationToken);

		if (rootFolders.Count == 0)
			return [new HealthIssue("warning", "rootFolders", "No root folders are configured")];

		var minimumFreeSpaceMb = (await _settings.GetMediaManagementConfigAsync()).MinimumFreeSpaceMb;
		var issues = new List<HealthIssue>();

		foreach (var rootFolder in rootFolders)
		{
			if (!Directory.Exists(rootFolder.Path))
			{
				issues.Add(new HealthIssue("error", "rootFolders", $"Root folder '{rootFolder.Path}' does not exist"));
				continue;
			}

			if (minimumFreeSpaceMb <= 0)
				continue;

			try
			{
				var freeSpaceMb = new DriveInfo(rootFolder.Path).AvailableFreeSpace / 1024 / 1024;

				if (freeSpaceMb < minimumFreeSpaceMb)
					issues.Add(new HealthIssue("warning", "rootFolders",
						$"Root folder '{rootFolder.Path}' has less than {minimumFreeSpaceMb} MB free space"));
			}
			catch (IOException)
			{
				// drive unavailable, nothing meaningful to report for this folder
			}
		}

		return issues;
	}

	private async Task<IEnumerable<HealthIssue>> CheckMetadataAsync(CancellationToken cancellationToken)
	{
		var reachable = await _metadataClient.PingAsync(cancellationToken);

		return reachable
			? Array.Empty<HealthIssue>()
			: [new HealthIssue("error", "metadata", "Metadata service is unreachable")];
	}

	private async Task<IEnumerable<HealthIssue>> CheckMappingsAsync(CancellationToken cancellationToken)
	{
		var reachable = await _mappingsClient.PingAsync(cancellationToken);

		return reachable
			? Array.Empty<HealthIssue>()
			: [new HealthIssue("error", "mappings", "Mappings service is unreachable")];
	}

	private async Task<IEnumerable<HealthIssue>> CheckRssAsync()
	{
		var indexerConfig = await _settings.GetIndexerConfigAsync();

		return indexerConfig.RssSyncIntervalMinutes == 0
			? [new HealthIssue("warning", "rss", "RSS sync is disabled")]
			: Array.Empty<HealthIssue>();
	}
}
