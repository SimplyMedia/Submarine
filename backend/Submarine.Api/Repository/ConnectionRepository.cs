using Submarine.Api.Models.Database;
using Submarine.Core.Notification;

namespace Submarine.Api.Repository;

/// <summary>
///     Connection Repository implementation
/// </summary>
public class ConnectionRepository : RepositoryBase<Connection>, IConnectionRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="ConnectionRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public ConnectionRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
