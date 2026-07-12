using Submarine.Api.Models.Database;
using Submarine.Core.Quality;

namespace Submarine.Api.Repository;

/// <summary>
///     Release Group Quality Override Repository implementation
/// </summary>
public class QualityOverrideRepository : RepositoryBase<ReleaseGroupQualityOverride>, IQualityOverrideRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="QualityOverrideRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public QualityOverrideRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
