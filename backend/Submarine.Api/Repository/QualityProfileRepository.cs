using Submarine.Api.Models.Database;
using Submarine.Core.Profile;

namespace Submarine.Api.Repository;

/// <summary>
///     Quality Profile Repository implementation
/// </summary>
public class QualityProfileRepository : RepositoryBase<QualityProfile>, IQualityProfileRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="QualityProfileRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public QualityProfileRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
