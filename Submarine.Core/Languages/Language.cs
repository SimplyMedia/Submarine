using System.Text.RegularExpressions;
using Submarine.Core.Attributes;
using Submarine.Core.Quality.Attributes;

namespace Submarine.Core.Languages;

public enum Language
{
	ENGLISH,

	[RegEx(@"(?:FR[A]?|french)")]
	FRENCH,

	[RegEx(@"spanish")]
	SPANISH,

	[RegEx(@"(?:ger|german|videomann|deu)\b")]
	GERMAN,

	[RegEx(@"\b(?:ita|italian)\b")]
	ITALIAN,

	[RegEx(@"danish")]
	DANISH,

	[RegEx(@"\b(?:dutch)\b")]
	DUTCH,

	[RegEx(@"\b(?:japanese|jp)\b")]
	JAPANESE,

	[RegEx(@"icelandic")]
	ICELANDIC,

	[RegEx(@"\[(?:CH[ST]|BIG5|GB)\]|简|繁|字幕|chinese|cantonese|mandarin")]
	CHINESE,

	[RegEx(@"\b(?:russian|rus)\b")]
	RUSSIAN,

	[RegEx(@"\b(?:PL\W?DUB|DUB\W?PL|LEK\W?PL|PL\W?LEK|polish|PL|POL)\b")]
	POLISH,

	[RegEx(@"vietnamese")]
	VIETNAMESE,

	[RegEx(@"swedish")]
	SWEDISH,

	[RegEx(@"norwegian")]
	NORWEGIAN,

	[RegEx(@"finnish")]
	FINNISH,

	[RegEx(@"turkish")]
	TURKISH,

	[RegEx(@"portuguese")]
	PORTUGUESE,

	[RegEx(@"flemish")]
	FLEMISH,

	[RegEx(@"greek")]
	GREEK,

	[RegEx(@"(?:KR|korean)")]
	KOREAN,

	[RegEx(@"\b(?:HUNDUB|HUN)\b")]
	HUNGARIAN,

	[RegEx(@"\bHebDub\b")]
	HEBREW,

	[RegEx(@"\b(?:lithuanian|LT)\b")]
	LITHUANIAN,

	[RegEx(@"\bCZ\b", RegexOptions.Compiled)]
	CZECH,

	[RegEx(@"arabic")]
	ARABIC,

	[RegEx(@"hindi")]
	HINDI
}
