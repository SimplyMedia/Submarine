using System;
using System.Text.RegularExpressions;

namespace Submarine.Core.Attributes;

/// <summary>
///     Attribute containing a regular expression
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class RegExAttribute : Attribute
{
	/// <summary>
	///     The regular expression
	/// </summary>
	public Regex Regex { get; }

	/// <summary>
	///     Creates a new instance of <see cref="RegExAttribute" />
	/// </summary>
	/// <param name="regex">The regex as string</param>
	/// <param name="options">Options of this Regex</param>
	public RegExAttribute(string regex, RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase)
		=> Regex = new Regex(regex, options);
}
