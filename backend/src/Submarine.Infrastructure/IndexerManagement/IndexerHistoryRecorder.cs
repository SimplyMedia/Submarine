using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Records indexer request history entries.
/// </summary>
/// <param name="db">The database context.</param>
/// <param name="timeProvider">The time source.</param>
public sealed class IndexerHistoryRecorder(SubmarineDbContext db, TimeProvider timeProvider)
{
	/// <summary>
	///     Records one history entry and persists it.
	/// </summary>
	public async Task RecordAsync(
		int indexerId,
		IndexerHistoryEventType eventType,
		bool successful,
		string? query,
		string? categories,
		string? source,
		int? elapsedMs,
		CancellationToken cancellationToken = default)
	{
		db.IndexerHistories.Add(new IndexerHistory
		{
			IndexerId = indexerId,
			EventType = eventType,
			Successful = successful,
			Query = query,
			Categories = categories,
			Source = source,
			ElapsedMs = elapsedMs,
			Date = timeProvider.GetUtcNow().UtcDateTime
		});

		await db.SaveChangesAsync(cancellationToken);
	}
}
