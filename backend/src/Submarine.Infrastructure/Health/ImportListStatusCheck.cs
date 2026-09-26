using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Checks import list failure backoff state, reporting lists currently disabled by repeated
///     fetch failures.
/// </summary>
public sealed class ImportListStatusCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "ImportLists";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var disabledUntil = await db.ImportListStatuses.AsNoTracking()
			.Where(x => x.DisabledUntil > now)
			.Join(db.ImportLists, status => status.ImportListId, list => list.Id,
				(status, list) => new { list.Name, status.DisabledUntil })
			.ToListAsync(cancellationToken);

		var issues = new List<HealthIssueSnapshot>();
		foreach (var status in disabledUntil)
		{
			issues.Add(new(HealthIssueType.WARNING, Source,
				$"Import list {status.Name} is disabled until {status.DisabledUntil:R} after repeated failures", null));
		}

		return issues;
	}
}
