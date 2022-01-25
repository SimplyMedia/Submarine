using Submarine.Core.Provider;

namespace Submarine.Core.Release.Usenet;

public record UsenetRelease : BaseRelease
{
	public UsenetRelease(BaseRelease release) : base(release)
		=> Protocol = Protocol.USENET;
}
