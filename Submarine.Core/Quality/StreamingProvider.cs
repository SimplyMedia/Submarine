using Submarine.Core.Attributes;
using Submarine.Core.Quality.Attributes;

namespace Submarine.Core.Quality;

public enum StreamingProvider
{
	[RegEx("(amzn|amazon)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	AMAZON,

	[RegEx("(nf|netflix)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	NETFLIX,

	[RegEx("(atvp|aptv)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	APPLE_TV,

	[RegEx("(hmax)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	HBO_MAX,

	[RegEx("(dsnp|dsny|disney|disney\\+)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	DISNEY,

	[RegEx("(hulu)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	HULU,

	[RegEx("(cr|crunchyroll|cr-dub)(?=[ ._\\-\\)](web[ ._-]?(dl|rip)?)?)")]
	CRUNCHYROLL,

	[RegEx("(funi|funidub|funimation)(?=[ ._-]web[ ._-]?(dl|rip)?)?")]
	FUNIMATION,

	[RegEx("(red)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	YOUTUBE_PREMIUM,

	[RegEx("(pcok)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	PEACOCK,

	[RegEx("(dcu)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	DC_UNIVERSE,

	[RegEx("(hbo)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	HBO_NOW
}
