using Submarine.Api.Models.Database;
using Submarine.Core.Tag;

namespace Submarine.Api.Repository;

/// <summary>
///     Tag Repository implementation
/// </summary>
public class TagRepository : RepositoryBase<Tag>, ITagRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="TagRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public TagRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
