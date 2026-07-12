using Submarine.Api.Models.Database;
using Submarine.Core.ImportList;

namespace Submarine.Api.Repository;

/// <summary>
///     Import List Repository implementation
/// </summary>
public class ImportListRepository : RepositoryBase<ImportList>, IImportListRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="ImportListRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public ImportListRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
