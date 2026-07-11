using Submarine.Api.Models.Database;
using Submarine.Core.Profile;

namespace Submarine.Api.Repository;

/// <summary>
///     Language Profile Repository implementation
/// </summary>
public class LanguageProfileRepository : RepositoryBase<LanguageProfile>, ILanguageProfileRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="LanguageProfileRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public LanguageProfileRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
