using System.Text.RegularExpressions;
using Submarine.Core.Attributes;

namespace Submarine.Core.Languages;

/// <summary>
///     Language of a release
/// </summary>
public enum Language
{
	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/English_language">English</see> language
	/// </summary>
	ENGLISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/French_language">French</see> language
	/// </summary>
	[RegEx("(?:FR[A]?|french)")]
	FRENCH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Spanish_language">Spanish</see> language
	/// </summary>
	[RegEx("spanish")]
	SPANISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/German_language">German</see> language
	/// </summary>
	[RegEx(@"(?:ger|german|videomann|deu)\b")]
	GERMAN,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Italian_language">Italian</see> language
	/// </summary>
	[RegEx(@"\b(?:ita|italian)\b")]
	ITALIAN,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Danish_language">Danish</see> language
	/// </summary>
	[RegEx("danish")]
	DANISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Dutch_language">Dutch</see> language
	/// </summary>
	[RegEx(@"\b(?:dutch)\b")]
	DUTCH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Japanese_language">Japanese</see> language
	/// </summary>
	[RegEx(@"\b(?:japanese|jp)\b")]
	JAPANESE,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Icelandic_language">Icelandic</see> language
	/// </summary>
	[RegEx("icelandic")]
	ICELANDIC,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Chinese_language">Chinese</see> language
	/// </summary>
	[RegEx(@"\[(?:CH[ST]|BIG5|GB)\]|简|繁|字幕|chinese|cantonese|mandarin")]
	CHINESE,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Russian_language">Russian</see> language
	/// </summary>
	[RegEx(@"\b(?:russian|rus)\b")]
	RUSSIAN,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Polish_language">Polish</see> language
	/// </summary>
	[RegEx(@"\b(?:PL\W?DUB|DUB\W?PL|LEK\W?PL|PL\W?LEK|polish|PL|POL)\b")]
	POLISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Vietnamese_language">Vietnamese</see> language
	/// </summary>
	[RegEx("vietnamese")]
	VIETNAMESE,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Swedish_language">Swedish</see> language
	/// </summary>
	[RegEx("swedish")]
	SWEDISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Norwegian_language">Norwegian</see> language
	/// </summary>
	[RegEx("norwegian")]
	NORWEGIAN,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Finnish_language">Finnish</see> language
	/// </summary>
	[RegEx("finnish")]
	FINNISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Turkish_language">Turkish</see> language
	/// </summary>
	[RegEx("turkish")]
	TURKISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Portuguese_language">Portuguese</see> language
	/// </summary>
	[RegEx("portuguese")]
	PORTUGUESE,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Flemish_dialects">Flemish</see> dialect for Dutch
	/// </summary>
	[RegEx("flemish")]
	FLEMISH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Greek_language">Greek</see> language
	/// </summary>
	[RegEx("greek")]
	GREEK,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Korean_language">Korean</see> language
	/// </summary>
	[RegEx("(?:KR|korean)")]
	KOREAN,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Hungarian_language">Hungarian</see> language
	/// </summary>
	[RegEx(@"\b(?:HUNDUB|HUN)\b")]
	HUNGARIAN,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Hebrew_language">Hebrew</see> language
	/// </summary>
	[RegEx(@"\bHebDub\b")]
	HEBREW,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Lithuanian_language">Lithuanian</see> language
	/// </summary>
	[RegEx(@"\b(?:lithuanian|LT)\b")]
	LITHUANIAN,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Czech_language">Czech</see> language
	/// </summary>
	[RegEx(@"\bCZ\b", RegexOptions.Compiled)]
	CZECH,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Arabic_language">Arabic</see> language
	/// </summary>
	[RegEx("arabic")]
	ARABIC,

	/// <summary>
	///     The <see href="https://en.wikipedia.org/wiki/Hindi">Hindi</see> language
	/// </summary>
	[RegEx("hindi")]
	HINDI
}
