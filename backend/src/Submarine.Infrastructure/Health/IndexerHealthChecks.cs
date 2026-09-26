using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Checks for indexers that started failing within the last 6 hours.
/// </summary>
public sealed class IndexerStatusHealthCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "Indexers";
	private static readonly TimeSpan RecentWindow = TimeSpan.FromHours(6);

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var total = await db.Indexers.AsNoTracking().CountAsync(cancellationToken);
		if (total == 0)
		{
			return [];
		}

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var names = await db.IndexerStatuses.AsNoTracking()
			.Where(x => x.DisabledUntil > now && x.InitialFailure != null && x.InitialFailure > now - RecentWindow)
			.Join(db.Indexers, status => status.IndexerId, indexer => indexer.Id, (status, indexer) => indexer.Name)
			.ToListAsync(cancellationToken);
		if (names.Count == 0)
		{
			return [];
		}

		return names.Count == total
			? [new(HealthIssueType.ERROR, Source, "All indexers are unavailable due to recent failures", HealthWikiLinks.For("indexers-unavailable-due-to-failures"))]
			: [new(HealthIssueType.WARNING, Source, $"Indexers unavailable due to recent failures: {string.Join(", ", names)}", HealthWikiLinks.For("indexers-unavailable-due-to-failures"))];
	}
}

/// <summary>
///     Checks for indexers that have been failing for more than 6 hours.
/// </summary>
public sealed class IndexerLongTermStatusHealthCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "Indexers";
	private static readonly TimeSpan RecentWindow = TimeSpan.FromHours(6);

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var total = await db.Indexers.AsNoTracking().CountAsync(cancellationToken);
		if (total == 0)
		{
			return [];
		}

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var names = await db.IndexerStatuses.AsNoTracking()
			.Where(x => x.DisabledUntil > now && x.InitialFailure != null && x.InitialFailure <= now - RecentWindow)
			.Join(db.Indexers, status => status.IndexerId, indexer => indexer.Id, (status, indexer) => indexer.Name)
			.ToListAsync(cancellationToken);
		if (names.Count == 0)
		{
			return [];
		}

		return names.Count == total
			? [new(HealthIssueType.ERROR, Source, "All indexers have been unavailable for more than 6 hours", HealthWikiLinks.For("indexers-unavailable-due-to-failures"))]
			: [new(HealthIssueType.WARNING, Source, $"Indexers unavailable for more than 6 hours: {string.Join(", ", names)}", HealthWikiLinks.For("indexers-unavailable-due-to-failures"))];
	}
}

/// <summary>
///     Checks that at least one indexer has RSS sync enabled and reachable.
/// </summary>
public sealed class IndexerRssHealthCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "Indexers";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var enabled = await db.Indexers.AsNoTracking().Where(x => x.EnableRss).Select(x => x.Id).ToListAsync(cancellationToken);
		if (enabled.Count == 0)
		{
			return [new(HealthIssueType.ERROR, Source,
				"No indexers have RSS sync enabled, new releases will not be grabbed automatically",
				HealthWikiLinks.For("no-indexers-with-rss-sync-enabled"))];
		}

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var backedOff = await db.IndexerStatuses.AsNoTracking()
			.Where(x => enabled.Contains(x.IndexerId) && x.DisabledUntil > now)
			.CountAsync(cancellationToken);
		return backedOff == enabled.Count
			? [new(HealthIssueType.WARNING, Source, "All indexers with RSS sync enabled are unavailable due to failures", HealthWikiLinks.For("indexers-unavailable-due-to-failures"))]
			: [];
	}
}

/// <summary>
///     Checks that indexers are available for automatic and interactive search.
/// </summary>
public sealed class IndexerSearchHealthCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "Indexers";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssueSnapshot>();

		var automatic = await db.Indexers.AsNoTracking().Where(x => x.EnableAutomaticSearch).Select(x => x.Id).ToListAsync(cancellationToken);
		if (automatic.Count == 0)
		{
			issues.Add(new(HealthIssueType.WARNING, Source,
				"No indexers have automatic search enabled", HealthWikiLinks.For("no-indexers-with-automatic-search-enabled")));
		}

		var interactiveCount = await db.Indexers.AsNoTracking().CountAsync(x => x.EnableInteractiveSearch, cancellationToken);
		if (interactiveCount == 0)
		{
			issues.Add(new(HealthIssueType.WARNING, Source,
				"No indexers have interactive search enabled", HealthWikiLinks.For("no-indexers-with-interactive-search-enabled")));
		}

		if (automatic.Count > 0)
		{
			var now = timeProvider.GetUtcNow().UtcDateTime;
			var backedOff = await db.IndexerStatuses.AsNoTracking()
				.Where(x => automatic.Contains(x.IndexerId) && x.DisabledUntil > now)
				.CountAsync(cancellationToken);
			if (backedOff == automatic.Count)
			{
				issues.Add(new(HealthIssueType.WARNING, Source,
					"All indexers with automatic search enabled are unavailable due to failures",
					HealthWikiLinks.For("indexers-unavailable-due-to-failures")));
			}
		}

		return issues;
	}
}

/// <summary>
///     Checks that enabled indexers reference a download client that still exists and is enabled.
/// </summary>
public sealed class IndexerDownloadClientHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Indexers";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var enabledClientIds = await db.DownloadClients.AsNoTracking().Where(x => x.Enable).Select(x => x.Id).ToListAsync(cancellationToken);
		var invalid = await db.Indexers.AsNoTracking()
			.Where(x => x.DownloadClientId != null && !enabledClientIds.Contains(x.DownloadClientId!.Value))
			.Select(x => x.Name)
			.ToListAsync(cancellationToken);

		return invalid.Count == 0
			? []
			: [new(HealthIssueType.WARNING, Source,
				$"Indexers use a download client that is missing or disabled: {string.Join(", ", invalid)}",
				HealthWikiLinks.For("invalid-indexer-download-client-setting"))];
	}
}

/// <summary>
///     Checks for Torznab indexers pointed at Jackett's aggregate "all" endpoint, which returns results
///     for every configured Jackett indexer instead of a single one and breaks per-indexer settings.
/// </summary>
public sealed class IndexerJackettAllHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Indexers";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var torznab = await db.Indexers.AsNoTracking()
			.Where(x => x.Implementation == IndexerImplementation.TORZNAB)
			.Select(x => new { x.Name, x.BaseUrl })
			.ToListAsync(cancellationToken);

		var jackettAll = torznab
			.Where(x => x.BaseUrl.Contains("/torznab/all/api", StringComparison.OrdinalIgnoreCase)
				|| x.BaseUrl.Contains("/api/v2.0/indexers/all/results/torznab", StringComparison.OrdinalIgnoreCase))
			.Select(x => x.Name)
			.ToList();

		return jackettAll.Count == 0
			? []
			: [new(HealthIssueType.WARNING, Source,
				$"Indexers use Jackett's aggregate \"all\" endpoint, add individual Jackett indexers instead: {string.Join(", ", jackettAll)}",
				HealthWikiLinks.For("jackett-all-endpoint-used"))];
	}
}
