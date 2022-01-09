using System.Text.RegularExpressions;

namespace Submarine.Core.Util.RegEx;

public class RegexReplace
{
	private readonly Regex _regex;
	private readonly string? _replacementFormat;
	private readonly MatchEvaluator? _replacementFunc;

	public RegexReplace(string pattern, string? replacement, RegexOptions regexOptions)
	{
		_regex = new Regex(pattern, regexOptions);
		_replacementFormat = replacement;
	}

	public RegexReplace(string pattern, MatchEvaluator replacement, RegexOptions regexOptions)
	{
		_regex = new Regex(pattern, regexOptions);
		_replacementFunc = replacement;
	}

	public string Replace(string input)
		=> _replacementFunc != null
			? _regex.Replace(input, _replacementFunc)
			: _regex.Replace(input, _replacementFormat!);

	public bool TryReplace(ref string input)
	{
		var result = _regex.IsMatch(input);
		input = _replacementFunc != null
			? _regex.Replace(input, _replacementFunc)
			: _regex.Replace(input, _replacementFormat!);
		return result;
	}
}
