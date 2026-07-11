using Submarine.Api.Models.Database;
using Submarine.Core.DecisionEngine.CustomFormats;

namespace Submarine.Api.Repository;

/// <summary>
///     Custom Format Repository implementation
/// </summary>
public class CustomFormatRepository : RepositoryBase<CustomFormatConfig>, ICustomFormatRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="CustomFormatRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public CustomFormatRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
