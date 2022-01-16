using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Attributes;
using Submarine.Core.Languages;
using Submarine.Core.Quality.Attributes;
using Submarine.Core.Util.Extensions;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser;

public class LanguageParserService : IParser<IReadOnlyList<Language>>
{
	private static readonly RegexReplace[] CleanSeriesTitleRegex =
	{
		new(@".*?[_. ](S\d{2}(?:E\d{2,4})*[_. ].*)", "$1",
			RegexOptions.Compiled | RegexOptions.IgnoreCase)
	};

	private readonly Dictionary<Language, Regex> _languageRegexes;

	private readonly ILogger<LanguageParserService> _logger;

	public LanguageParserService(ILogger<LanguageParserService> logger)
	{
		_logger = logger;
		_languageRegexes = new Dictionary<Language, Regex>();
		foreach (var language in Enum.GetValues<Language>())
		{
			var regex = language.GetAttribute<RegExAttribute>()?.Regex;

			if (regex == null)
			{
				_logger.LogDebug("No Regex defined for {Language}, ignoring for now", language);
				continue;
			}

			_languageRegexes.Add(language, regex);
		}
	}

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
		var set = new HashSet<Language>();

		foreach (var (language, regex) in _languageRegexes)
		{
			if (!regex.IsMatch(input)) continue;
			_logger.LogDebug("{Input} matched language regex for {Language}", input, language);
			set.Add(language);
		}

		return set;
	}
}
