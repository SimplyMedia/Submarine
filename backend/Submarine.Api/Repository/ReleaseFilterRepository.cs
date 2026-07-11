using Submarine.Api.Models.Database;
using Submarine.Core.DecisionEngine.Filter;

namespace Submarine.Api.Repository;

/// <summary>
///     Release Filter Repository implementation
/// </summary>
public class ReleaseFilterRepository : RepositoryBase<ReleaseFilterConfig>, IReleaseFilterRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="ReleaseFilterRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public ReleaseFilterRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
