using Submarine.Api.Models.Database;
using Submarine.Core.Download;

namespace Submarine.Api.Repository;

/// <summary>
///     Remote Path Mapping Repository implementation
/// </summary>
public class RemotePathMappingRepository : RepositoryBase<RemotePathMapping>, IRemotePathMappingRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="RemotePathMappingRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public RemotePathMappingRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
