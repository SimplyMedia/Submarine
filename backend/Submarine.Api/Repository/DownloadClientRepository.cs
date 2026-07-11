using Submarine.Api.Models.Database;
using Submarine.Core.Download;

namespace Submarine.Api.Repository;

/// <summary>
///     Download Client Repository implementation
/// </summary>
public class DownloadClientRepository : RepositoryBase<DownloadClientConfig>, IDownloadClientRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="DownloadClientRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public DownloadClientRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
