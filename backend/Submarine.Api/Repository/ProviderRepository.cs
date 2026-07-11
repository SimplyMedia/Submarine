using Submarine.Api.Models.Database;
using Submarine.Core.Provider;

namespace Submarine.Api.Repository;

/// <summary>
///     Provider Repository implementation
/// </summary>
public class ProviderRepository : RepositoryBase<Provider>, IProviderRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="ProviderRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public ProviderRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
