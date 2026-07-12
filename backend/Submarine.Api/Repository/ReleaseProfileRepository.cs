using Submarine.Api.Models.Database;
using Submarine.Core.Profile;

namespace Submarine.Api.Repository;

/// <summary>
///     Release Profile Repository implementation
/// </summary>
public class ReleaseProfileRepository : RepositoryBase<ReleaseProfile>, IReleaseProfileRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="ReleaseProfileRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public ReleaseProfileRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
