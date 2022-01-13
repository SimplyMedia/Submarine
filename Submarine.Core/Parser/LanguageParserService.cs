using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Languages;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser;

public class LanguageParserService : IParser<IReadOnlyList<Language>>
{
	private static readonly RegexReplace[] CleanSeriesTitleRegex =
	{
		new(@".*?[_. ](S\d{2}(?:E\d{2,4})*[_. ].*)", "$1",
			RegexOptions.Compiled | RegexOptions.IgnoreCase)
	};

	private static readonly Regex LanguageRegex = new(
		@"(?<french>(?:FR[A]?|french))|
(?<spanish>spanish)|
(?<german>(?:ger|german|videomann|deu)\b)|
(?<italian>\b(?:ita|italian)\b)|
(?<danish>danish)|
(?<dutch>\b(?:dutch)\b)|
(?<japanese>japanese)|
(?<icelandic>icelandic)|
(?<chinese>\[(?:CH[ST]|BIG5|GB)\]|简|繁|字幕|chinese|cantonese|mandarin)|
(?<russian>\b(?:russian|rus)\b)|
(?<polish>\b(?:PL\W?DUB|DUB\W?PL|LEK\W?PL|PL\W?LEK|polish|PL|POL)\b)|
(?<vietnamese>vietnamese)|
(?<swedish>swedish)|
(?<norwegian>Norwegian)|
(?<finnish>finnish)|
(?<turkish>turkish)|
(?<portuguese>portuguese)|
(?<flemish>flemish)|
(?<greek>greek)|
(?<korean>korean)|
(?<hungarian>\b(?:HUNDUB|HUN)\b)|
(?<hebrew>\bHebDub\b)|
(?<lithuanian>\b(?:lithuanian|LT)\b)|
(?<arabic>arabic)|
(?<hindi>hindi)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

	private static readonly Regex CaseSensitiveLanguageRegex = new(
		@"(?<czech>\bCZ\b)",
		RegexOptions.Compiled);

	private readonly ILogger<LanguageParserService> _logger;

	public LanguageParserService(ILogger<LanguageParserService> logger)
		=> _logger = logger;

	public IReadOnlyList<Language> Parse(string input)
	{
		_logger.LogDebug("Trying to parse language for {Input}", input);

		foreach (var regex in CleanSeriesTitleRegex)
			if (regex.TryReplace(input, out input))
				break;

		var parsed = MatchLanguages(input);

		if (parsed.Any()) return parsed.ToList();

		_logger.LogDebug("{Input} didn't match any Language, fallback to default 'English'", input);

		return new[] { Language.ENGLISH };
	}

	private HashSet<Language> MatchLanguages(string input)
	{
		var list = new HashSet<Language>();

		// Case sensitive
		var caseSensitiveMatches = CaseSensitiveLanguageRegex.Matches(input);

		foreach (Match match in caseSensitiveMatches)
			if (match.Groups["czech"].Captures.Any())
			{
				_logger.LogDebug("{Input} matched case sensitive language regex for Czech", input);
				list.Add(Language.CZECH);
			}

		// Case insensitive
		var matches = LanguageRegex.Matches(input);

		foreach (Match match in matches)
		{
			if (match.Groups["french"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for French", input);
				list.Add(Language.FRENCH);
			}

			if (match.Groups["spanish"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Spanish", input);
				list.Add(Language.SPANISH);
			}

			if (match.Groups["german"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for German", input);
				list.Add(Language.GERMAN);
			}

			if (match.Groups["italian"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Italian", input);
				list.Add(Language.ITALIAN);
			}

			if (match.Groups["danish"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Danish", input);
				list.Add(Language.DANISH);
			}

			if (match.Groups["dutch"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Dutch", input);
				list.Add(Language.DUTCH);
			}

			if (match.Groups["japanese"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Japanese", input);
				list.Add(Language.JAPANESE);
			}

			if (match.Groups["icelandic"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Icelandic", input);
				list.Add(Language.ICELANDIC);
			}

			if (match.Groups["chinese"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Chinese", input);
				list.Add(Language.CHINESE);
			}

			if (match.Groups["russian"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Russian", input);
				list.Add(Language.RUSSIAN);
			}

			if (match.Groups["polish"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Polish", input);
				list.Add(Language.POLISH);
			}

			if (match.Groups["vietnamese"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Vietnamese", input);
				list.Add(Language.VIETNAMESE);
			}

			if (match.Groups["swedish"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Swedish", input);
				list.Add(Language.SWEDISH);
			}

			if (match.Groups["norwegian"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Norwegian", input);
				list.Add(Language.NORWEGIAN);
			}

			if (match.Groups["finnish"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Finnish", input);
				list.Add(Language.FINNISH);
			}

			if (match.Groups["turkish"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Turkish", input);
				list.Add(Language.TURKISH);
			}

			if (match.Groups["portuguese"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Portuguese", input);
				list.Add(Language.PORTUGUESE);
			}

			if (match.Groups["flemish"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Flemish", input);
				list.Add(Language.FLEMISH);
			}

			if (match.Groups["greek"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Greek", input);
				list.Add(Language.GREEK);
			}

			if (match.Groups["korean"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Korean", input);
				list.Add(Language.KOREAN);
			}

			if (match.Groups["hungarian"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Hungarian", input);
				list.Add(Language.HUNGARIAN);
			}

			if (match.Groups["hebrew"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Hebrew", input);
				list.Add(Language.HEBREW);
			}

			if (match.Groups["lithuanian"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Lithuanian", input);
				list.Add(Language.LITHUANIAN);
			}

			if (match.Groups["arabic"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Arabic", input);
				list.Add(Language.ARABIC);
			}

			if (match.Groups["hindi"].Success)
			{
				_logger.LogDebug("{Input} matched case insensitive language regex for Hindi", input);
				list.Add(Language.HINDI);
			}
		}

		return list;
	}
}
