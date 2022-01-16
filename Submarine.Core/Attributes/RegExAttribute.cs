using System;
using System.Text.RegularExpressions;

namespace Submarine.Core.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class RegExAttribute : Attribute
{
	public Regex Regex { get; }

	public RegExAttribute(string regex, RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase)
		=> Regex = new Regex(regex, options);
}
