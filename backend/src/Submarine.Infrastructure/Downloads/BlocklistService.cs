using Microsoft.EntityFrameworkCore;
using Submarine.Core.DecisionEngine;
using Submarine.Core.MediaFiles;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     Checks releases against the persisted blocklist.
/// </summary>
public sealed class BlocklistService(SubmarineDbContext db) : IBlocklistService
{
	/// <inheritdoc />
	public async Task<bool> IsBlocklistedAsync(
		string releaseTitle,
		string? guid,
		int? indexerId,
		string? infoHash,
		CancellationToken cancellationToken = default)
	{
		if (guid is not null && await db.BlocklistItems.AnyAsync(item => item.Guid == guid, cancellationToken))
		{
			return true;
		}

		if (infoHash is not null && await db.BlocklistItems.AnyAsync(item => item.Guid == infoHash, cancellationToken))
		{
			return true;
		}

		return await db.BlocklistItems.AnyAsync(
			item => item.IndexerId == indexerId && item.ReleaseTitle == releaseTitle,
			cancellationToken);
	}

	/// <inheritdoc />
	public async Task<Func<ReleaseCandidate, bool>> BuildPredicateAsync(CancellationToken cancellationToken = default)
	{
		var items = await db.BlocklistItems.AsNoTracking().ToListAsync(cancellationToken);
		var guids = items.Where(item => item.Guid is not null).Select(item => item.Guid!).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var byTitleAndIndexer = items
			.Select(item => (item.IndexerId, Title: item.ReleaseTitle))
			.ToHashSet();

		return candidate =>
			(candidate.Info.Guid is { Length: > 0 } && guids.Contains(candidate.Info.Guid))
			|| (candidate.Info.InfoHash is not null && guids.Contains(candidate.Info.InfoHash))
			|| byTitleAndIndexer.Contains((candidate.Info.IndexerId, candidate.Info.Title));
	}
}
