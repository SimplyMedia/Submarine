using Submarine.Core.Provider;

namespace Submarine.Core.Release.Usenet;

/// <summary>
///     Usenet Release
/// </summary>
public record UsenetRelease : BaseRelease
{
	/// <summary>
	///     Creates a new instance of <see cref="UsenetRelease" /> from a <see cref="BaseRelease" />
	/// </summary>
	/// <param name="release">The base release</param>
	public UsenetRelease(BaseRelease release) : base(release)
		=> Protocol = Protocol.USENET;
}
