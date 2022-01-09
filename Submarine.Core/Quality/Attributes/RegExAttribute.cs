using System;
using System.Text.RegularExpressions;

namespace Submarine.Core.Quality.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class RegExAttribute : Attribute
{
	public Regex Regex { get; }

	public RegExAttribute(string regex, bool ignoreCase = true)
		=> Regex = new Regex(regex, ignoreCase
			? RegexOptions.Compiled | RegexOptions.IgnoreCase
			: RegexOptions.Compiled);
}
