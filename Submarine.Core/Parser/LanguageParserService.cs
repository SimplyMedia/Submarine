using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Languages;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser
{
	public class LanguageParserService : IParser<IReadOnlyList<Language>>
	{
		private static readonly RegexReplace[] CleanSeriesTitleRegex = new[]
		{
			new RegexReplace(@".*?[_. ](S\d{2}(?:E\d{2,4})*[_. ].*)", "$1",
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
(?<polish>\b(?:PL\W?DUB|DUB\W?PL|LEK\W?PL|PL\W?LEK|polish|PL)\b)|
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
			{
				if (regex.TryReplace(ref input))
					break;
			}

			var parsed = GetLanguageFromRegEx(input);

			return parsed.Any() 
				? parsed.ToImmutableList()
				: new [] { Language.ENGLISH };
		}

		private static HashSet<Language> GetLanguageFromRegEx(string title)
		{
			var list = new HashSet<Language>();
			
			// Case sensitive
			var caseSensitiveMatches = CaseSensitiveLanguageRegex.Matches(title);

			foreach (Match match in caseSensitiveMatches)
			{
				if (match.Groups["czech"].Captures.Any())
					list.Add(Language.CZECH);
			}

			// Case insensitive
			var matches = LanguageRegex.Matches(title);

			foreach (Match match in matches)
			{
				if (match.Groups["french"].Success)
					list.Add(Language.FRENCH);

				if (match.Groups["spanish"].Success)
					list.Add(Language.SPANISH);
				
				if (match.Groups["german"].Success)
					list.Add(Language.GERMAN);
				
				if (match.Groups["italian"].Success)
					list.Add(Language.ITALIAN);

				if (match.Groups["danish"].Success)
					list.Add(Language.DANISH);

				if (match.Groups["dutch"].Success)
					list.Add(Language.DUTCH);

				if (match.Groups["japanese"].Success)
					list.Add(Language.JAPANESE);

				if (match.Groups["icelandic"].Success)
					list.Add(Language.ICELANDIC);

				if (match.Groups["chinese"].Success)
					list.Add(Language.CHINESE);

				if (match.Groups["russian"].Success)
					list.Add(Language.RUSSIAN);

				if (match.Groups["polish"].Success)
					list.Add(Language.POLISH);

				if (match.Groups["vietnamese"].Success)
					list.Add(Language.VIETNAMESE);

				if (match.Groups["swedish"].Success)
					list.Add(Language.SWEDISH);

				if (match.Groups["norwegian"].Success)
					list.Add(Language.NORWEGIAN);

				if (match.Groups["finnish"].Success)
					list.Add(Language.FINNISH);

				if (match.Groups["turkish"].Success)
					list.Add(Language.TURKISH);

				if (match.Groups["portuguese"].Success)
					list.Add(Language.PORTUGUESE);

				if (match.Groups["flemish"].Success)
					list.Add(Language.FLEMISH);

				if (match.Groups["greek"].Success)
					list.Add(Language.GREEK);

				if (match.Groups["korean"].Success)
					list.Add(Language.KOREAN);

				if (match.Groups["hungarian"].Success)
					list.Add(Language.HUNGARIAN);

				if (match.Groups["hebrew"].Success)
					list.Add(Language.HEBREW);

				if (match.Groups["lithuanian"].Success)
					list.Add(Language.LITHUANIAN);

				if (match.Groups["arabic"].Success)
					list.Add(Language.ARABIC);

				if (match.Groups["hindi"].Success)
					list.Add(Language.HINDI);
			}
			
			return list;
		}
	}
}
