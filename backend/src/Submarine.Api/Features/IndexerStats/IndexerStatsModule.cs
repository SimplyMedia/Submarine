using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.IndexerStats;

/// <summary>
///     Aggregates indexer request history into per-indexer and per-caller (user agent) statistics.
/// </summary>
public sealed class IndexerStatsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
		=> endpoints.MapGet("/api/v1/indexer-stats", GetAsync);

	private static async Task<Ok<IndexerStatsResponse>> GetAsync(
		SubmarineDbContext db,
		[FromQuery] DateTime? start,
		[FromQuery] DateTime? end,
		[FromQuery] string? indexerIds,
		CancellationToken cancellationToken)
	{
		var rangeStart = start ?? DateTime.UtcNow.AddDays(-30);
		var rangeEnd = end ?? DateTime.UtcNow;
		var ids = string.IsNullOrWhiteSpace(indexerIds)
			? null
			: indexerIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(int.Parse)
				.ToHashSet();

		var query = db.IndexerHistories.AsNoTracking().Where(entry => entry.Date >= rangeStart && entry.Date <= rangeEnd);
		if (ids is not null)
		{
			query = query.Where(entry => ids.Contains(entry.IndexerId));
		}

		var entries = await query.ToListAsync(cancellationToken);
		var indexerNames = await db.Indexers.AsNoTracking().ToDictionaryAsync(indexer => indexer.Id, indexer => indexer.Name, cancellationToken);

		var perIndexer = entries
			.GroupBy(entry => entry.IndexerId)
			.Select(group => new IndexerStatEntry(
				group.Key,
				indexerNames.GetValueOrDefault(group.Key, "Unknown"),
				group.Count(entry => entry.EventType is IndexerHistoryEventType.QUERY or IndexerHistoryEventType.RSS),
				group.Count(entry => entry.EventType == IndexerHistoryEventType.GRAB),
				group.Count(entry => !entry.Successful),
				group.Any(entry => entry.ElapsedMs.HasValue) ? group.Where(entry => entry.ElapsedMs.HasValue).Average(entry => entry.ElapsedMs!.Value) : 0))
			.OrderBy(entry => entry.IndexerName)
			.ToList();

		const string userAgentPrefix = "newznab:";
		var perUserAgent = entries
			.Where(entry => entry.Source?.StartsWith(userAgentPrefix, StringComparison.Ordinal) == true)
			.GroupBy(entry => entry.Source![userAgentPrefix.Length..])
			.Select(group => new UserAgentStatEntry(
				group.Key,
				group.Count(entry => entry.EventType is IndexerHistoryEventType.QUERY or IndexerHistoryEventType.RSS),
				group.Count(entry => entry.EventType == IndexerHistoryEventType.GRAB)))
			.OrderByDescending(entry => entry.QueryCount)
			.ToList();

		return TypedResults.Ok(new IndexerStatsResponse(perIndexer, perUserAgent));
	}
}

/// <summary>Aggregated request statistics for one indexer.</summary>
public sealed record IndexerStatEntry(int IndexerId, string IndexerName, int QueryCount, int GrabCount, int FailureCount, double AverageResponseMs);

/// <summary>Aggregated request statistics for one caller of the outbound Newznab API, identified by its user agent.</summary>
public sealed record UserAgentStatEntry(string UserAgent, int QueryCount, int GrabCount);

/// <summary>Indexer request statistics within a date range.</summary>
public sealed record IndexerStatsResponse(IReadOnlyList<IndexerStatEntry> Indexers, IReadOnlyList<UserAgentStatEntry> UserAgents);
