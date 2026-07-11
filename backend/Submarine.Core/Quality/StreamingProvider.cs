using Submarine.Core.Attributes;

namespace Submarine.Core.Quality;

/// <summary>
///     Streaming Provider (or commonly known as Streaming Service) are services that provide content over the internet
/// </summary>
public enum StreamingProvider
{
	/// <summary>
	///     Amazon Prime Video <see href="https://en.wikipedia.org/wiki/Amazon_Prime_Video" />
	/// </summary>
	[RegEx("(amzn|amazon)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	AMAZON,

	/// <summary>
	///     Netflix <see href="https://en.wikipedia.org/wiki/Netflix" />
	/// </summary>
	[RegEx("(nf|netflix)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	NETFLIX,

	/// <summary>
	///     Apple TV+ <see href="https://en.wikipedia.org/wiki/Apple_TV%2B" />
	/// </summary>
	[RegEx("(atvp|aptv)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	APPLE_TV,

	/// <summary>
	///     HBO Max <see href="https://en.wikipedia.org/wiki/HBO_Max" />
	/// </summary>
	[RegEx("(hmax)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	HBO_MAX,

	/// <summary>
	///     Disney Plus <see href="https://en.wikipedia.org/wiki/Disney%2B" />
	/// </summary>
	[RegEx("(dp|dsnp|dsny|disney|disney\\+)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	DISNEY,

	/// <summary>
	///     Hulu <see href="https://en.wikipedia.org/wiki/Hulu" />
	/// </summary>
	[RegEx("(hulu)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	HULU,

	/// <summary>
	///     Crunchyroll <see href="https://en.wikipedia.org/wiki/Crunchyroll" />
	/// </summary>
	[RegEx(@"(cr|crunchyroll|cr-dub)(?=[ ._\-\)](web[ ._-]?(dl|rip)?)?)")]
	CRUNCHYROLL,

	/// <summary>
	///     Funimation <see href="https://en.wikipedia.org/wiki/Funimation" />
	/// </summary>
	[RegEx("(funi|funidub|funimation)(?=[ ._-]web[ ._-]?(dl|rip)?)?")]
	FUNIMATION,

	/// <summary>
	///     YouTube Premium <see href="https://en.wikipedia.org/wiki/YouTube_Premium" />
	/// </summary>
	[RegEx("(red)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	YOUTUBE_PREMIUM,

	/// <summary>
	///     Peacock <see href="https://en.wikipedia.org/wiki/Peacock_(streaming_service)" />
	/// </summary>
	[RegEx("(pcok)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	PEACOCK,

	/// <summary>
	///     DC Universe <see href="https://en.wikipedia.org/wiki/DC_Universe_(streaming_service)" />
	/// </summary>
	[RegEx("(dcu)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	DC_UNIVERSE,

	/// <summary>
	///     HBO Now <see href="https://en.wikipedia.org/wiki/HBO_Now" />
	/// </summary>
	[RegEx("(hbo)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	HBO_NOW,

	/// <summary>
	///     Paramount+ <see href="https://en.wikipedia.org/wiki/Paramount%2B" />
	/// </summary>
	[RegEx("(pmtp)(?=[ ._-]web[ ._-]?(dl|rip)?)")]
	PARAMOUNT_PLUS,

	/// <summary>
	///     Comedy Central <see href="https://en.wikipedia.org/wiki/Comedy_Central" />
	/// </summary>
	[RegEx(@"\b(CC)\b[ ._-]web[ ._-]?(dl|rip)?\b")]
	COMEDY_CENTRAL,

	/// <summary>
	///     Crave <see href="https://en.wikipedia.org/wiki/Crave_(streaming_service)" />
	/// </summary>
	[RegEx(@"\b(crav(e)?)\b[ ._-]web[ ._-]?(dl|rip)?\b")]
	CRAVE,

	/// <summary>
	///     HiDive <see href="https://en.wikipedia.org/wiki/Sentai_Filmworks#Hidive" />
	/// </summary>
	[RegEx(@"\b(HIDI(VE)?)\b")]
	HIDIVE,

	/// <summary>
	///     Itunes <see href="https://en.wikipedia.org/wiki/ITunes" />
	/// </summary>
	[RegEx(@"\b(it|itunes)\b(?=[ ._-]web[ ._-]?(dl|rip)\b)")]
	ITUNES,

	/// <summary>
	///     Movies Anywhere <see href="https://en.wikipedia.org/wiki/Movies_Anywhere" />
	/// </summary>
	[RegEx(@"(?<!dts[ .-]?hd[ .-]?)ma\b(?=.*\bweb[ ._-]?(dl|rip)\b)")]
	MOVIES_ANYWHERE
}
