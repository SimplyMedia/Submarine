using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Warns when a VIP indexer's membership expires within 7 days, and errors once it has expired. Mirrors
///     Prowlarr's IndexerVIPCheck and IndexerVIPExpiredCheck, folded into one check since both key off the same
///     <see cref="Submarine.Core.Entities.Indexer.VipExpiration" /> field.
/// </summary>
public sealed class IndexerVipHealthCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "Indexers";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssueSnapshot>();
		var indexers = await db.Indexers.AsNoTracking()
			.Where(indexer => indexer.VipExpiration != null)
			.Select(indexer => new { indexer.Name, indexer.VipExpiration })
			.ToListAsync(cancellationToken);

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var expiring = new List<string>();
		var expired = new List<string>();

		foreach (var indexer in indexers)
		{
			if (!DateTime.TryParse(indexer.VipExpiration, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration))
			{
				continue;
			}

			if (expiration < now)
			{
				expired.Add(indexer.Name);
			}
			else if (expiration <= now.AddDays(7))
			{
				expiring.Add(indexer.Name);
			}
		}

		if (expiring.Count > 0)
		{
			issues.Add(new HealthIssueSnapshot(HealthIssueType.WARNING, Source,
				$"VIP membership expires within 7 days for: {string.Join(", ", expiring)}", null));
		}

		if (expired.Count > 0)
		{
			issues.Add(new HealthIssueSnapshot(HealthIssueType.ERROR, Source,
				$"VIP membership has expired for: {string.Join(", ", expired)}", null));
		}

		return issues;
	}
}
