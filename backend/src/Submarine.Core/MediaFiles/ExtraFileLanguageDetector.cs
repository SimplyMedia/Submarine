using System.Text.RegularExpressions;
using Submarine.Core.Languages;

namespace Submarine.Core.MediaFiles;

/// <summary>
///     Detects a language suffix on extra files imported alongside a media file, for example
///     "Movie.en.srt" or "Episode.fre.srt".
/// </summary>
public static partial class ExtraFileLanguageDetector
{
	private static readonly IReadOnlyDictionary<string, Language> CodeMap = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
	{
		["en"] = Language.ENGLISH,
		["eng"] = Language.ENGLISH,
		["fr"] = Language.FRENCH,
		["fre"] = Language.FRENCH,
		["fra"] = Language.FRENCH,
		["es"] = Language.SPANISH,
		["spa"] = Language.SPANISH,
		["de"] = Language.GERMAN,
		["ger"] = Language.GERMAN,
		["deu"] = Language.GERMAN,
		["it"] = Language.ITALIAN,
		["ita"] = Language.ITALIAN,
		["da"] = Language.DANISH,
		["dan"] = Language.DANISH,
		["nl"] = Language.DUTCH,
		["dut"] = Language.DUTCH,
		["nld"] = Language.DUTCH,
		["ja"] = Language.JAPANESE,
		["jpn"] = Language.JAPANESE,
		["is"] = Language.ICELANDIC,
		["isl"] = Language.ICELANDIC,
		["zh"] = Language.CHINESE,
		["chi"] = Language.CHINESE,
		["zho"] = Language.CHINESE,
		["ru"] = Language.RUSSIAN,
		["rus"] = Language.RUSSIAN,
		["pl"] = Language.POLISH,
		["pol"] = Language.POLISH,
		["vi"] = Language.VIETNAMESE,
		["vie"] = Language.VIETNAMESE,
		["sv"] = Language.SWEDISH,
		["swe"] = Language.SWEDISH,
		["no"] = Language.NORWEGIAN,
		["nor"] = Language.NORWEGIAN,
		["fi"] = Language.FINNISH,
		["fin"] = Language.FINNISH,
		["tr"] = Language.TURKISH,
		["tur"] = Language.TURKISH,
		["pt"] = Language.PORTUGUESE,
		["por"] = Language.PORTUGUESE,
		["el"] = Language.GREEK,
		["gre"] = Language.GREEK,
		["ell"] = Language.GREEK,
		["ko"] = Language.KOREAN,
		["kor"] = Language.KOREAN,
		["hu"] = Language.HUNGARIAN,
		["hun"] = Language.HUNGARIAN,
		["he"] = Language.HEBREW,
		["heb"] = Language.HEBREW,
		["lt"] = Language.LITHUANIAN,
		["lit"] = Language.LITHUANIAN,
		["cs"] = Language.CZECH,
		["cze"] = Language.CZECH,
		["ces"] = Language.CZECH,
		["ar"] = Language.ARABIC,
		["ara"] = Language.ARABIC,
		["hi"] = Language.HINDI,
		["hin"] = Language.HINDI
	};

	/// <summary>
	///     Detects a language suffix on a file name with its final extension already removed, for example
	///     "Movie.en" or "Episode.fre". Returns null when no recognized suffix is present.
	/// </summary>
	public static Language? Detect(string nameWithoutExtension)
	{
		var match = SuffixRegex().Match(nameWithoutExtension);
		return match.Success && CodeMap.TryGetValue(match.Groups["code"].Value, out var language)
			? language
			: null;
	}

	[GeneratedRegex(@"[._\-](?<code>[a-zA-Z]{2,3})$")]
	private static partial Regex SuffixRegex();
}
