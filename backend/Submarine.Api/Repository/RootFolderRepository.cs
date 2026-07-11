using Submarine.Api.Models.Database;
using Submarine.Core.Library;

namespace Submarine.Api.Repository;

/// <summary>
///     Root Folder Repository implementation
/// </summary>
public class RootFolderRepository : RepositoryBase<RootFolder>, IRootFolderRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="RootFolderRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public RootFolderRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
