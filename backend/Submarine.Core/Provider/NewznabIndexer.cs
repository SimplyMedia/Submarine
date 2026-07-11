using System.Collections.Generic;

namespace Submarine.Core.Provider;

/// <summary>
///     A Newznab Indexer is a <see cref="Protocol.USENET" /> Provider speaking the Newznab API
/// </summary>
public class NewznabIndexer : Provider
{
	/// <summary>
	///     Category ids queried for standard Releases
	/// </summary>
	public List<int> Categories { get; set; } = new();

	/// <summary>
	///     Category ids queried for Anime Releases
	/// </summary>
	public List<int> AnimeCategories { get; set; } = new();

	/// <summary>
	///     Creates a new instance of <see cref="NewznabIndexer" />
	/// </summary>
	public NewznabIndexer()
		=> Protocol = Protocol.USENET;
}
