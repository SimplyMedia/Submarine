using System;
using System.Text.RegularExpressions;

namespace Submarine.Core.Quality.Attributes
{
	[AttributeUsage(AttributeTargets.Field)]
	public class RegExAttribute : Attribute
	{
		public Regex Regex { get; }

		public RegExAttribute(string regex)
			=> Regex = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
	}
}
