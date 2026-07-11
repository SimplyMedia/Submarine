namespace Submarine.Core.Provider;

/// <summary>
///     A Usenet Indexer is a Provider which serves <see href="https://en.wikipedia.org/wiki/NZB">NZB</see> files to
///     download via <see cref="Protocol.USENET" />
/// </summary>
public class UsenetIndexer : Provider
{
	/// <summary>
	///     Creates a new instance of <see cref="UsenetIndexer" />
	/// </summary>
	public UsenetIndexer()
		=> Protocol = Protocol.USENET;
}
