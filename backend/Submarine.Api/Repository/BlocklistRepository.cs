using Submarine.Api.Models.Database;
using Submarine.Core.Download;

namespace Submarine.Api.Repository;

/// <summary>
///     Blocklist Repository implementation
/// </summary>
public class BlocklistRepository : RepositoryBase<BlocklistItem>, IBlocklistRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="BlocklistRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public BlocklistRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
