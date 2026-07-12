using Submarine.Api.Models.Database;
using Submarine.Core.Profile;

namespace Submarine.Api.Repository;

/// <summary>
///     Delay Profile Repository implementation
/// </summary>
public class DelayProfileRepository : RepositoryBase<DelayProfile>, IDelayProfileRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="DelayProfileRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public DelayProfileRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
