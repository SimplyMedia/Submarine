using System.Collections.Generic;

namespace Submarine.Core.Indexers;

/// <summary>
///     A node of the standard Newznab category tree
/// </summary>
/// <param name="Id">The numeric category id, e.g. 5040</param>
/// <param name="Name">The human readable name, e.g. TV/HD</param>
/// <param name="SubCategories">The child categories</param>
public record IndexerCategory(int Id, string Name, List<IndexerCategory> SubCategories)
{
	/// <inheritdoc />
	public override string ToString()
		=> Name;
}
