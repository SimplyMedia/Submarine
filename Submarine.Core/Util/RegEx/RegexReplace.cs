using System.Text.RegularExpressions;

namespace Submarine.Core.Util.RegEx;

/// <summary>
///     Helper class for Regex with build in Replace functionality
/// </summary>
public class RegexReplace
{
	private readonly Regex _regex;
	private readonly string? _replacementFormat;
	private readonly MatchEvaluator? _replacementFunc;

	/// <summary>
	///     Creates a new instance of <see cref="RegexReplace" /> with string replacement
	/// </summary>
	/// <param name="pattern">The pattern of the regex</param>
	/// <param name="replacement">The replacement string</param>
	/// <param name="regexOptions">options for the regex</param>
	public RegexReplace(string pattern, string? replacement, RegexOptions regexOptions)
	{
		_regex = new Regex(pattern, regexOptions);
		_replacementFormat = replacement;
	}

	/// <summary>
	///     Creates a new instance of <see cref="RegexReplace" /> with MatchEvaluator replacement
	/// </summary>
	/// <param name="pattern">The pattern of the regex</param>
	/// <param name="replacement">replacement function</param>
	/// <param name="regexOptions">options for the regex</param>
	public RegexReplace(string pattern, MatchEvaluator replacement, RegexOptions regexOptions)
	{
		_regex = new Regex(pattern, regexOptions);
		_replacementFunc = replacement;
	}

	/// <summary>
	///     Replace with this <see cref="RegexReplace" />
	/// </summary>
	/// <param name="input">The input to replace</param>
	/// <returns>Replaced string</returns>
	public string Replace(string input)
		=> _replacementFunc != null
			? _regex.Replace(input, _replacementFunc)
			: _regex.Replace(input, _replacementFormat!);

	/// <summary>
	///     Try to replace with this <see cref="RegexReplace" />
	/// </summary>
	/// <param name="input">The input to replace</param>
	/// <param name="replaced">Replaced string</param>
	/// <returns>If anything was replaced in <see cref="input" /></returns>
	public bool TryReplace(string input, out string replaced)
	{
		var result = _regex.IsMatch(input);
		replaced = _replacementFunc != null
			? _regex.Replace(input, _replacementFunc)
			: _regex.Replace(input, _replacementFormat!);
		return result;
	}
}
