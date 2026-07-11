using System;
using System.Globalization;
using System.Text;

namespace Submarine.Core.Util.Extensions;

/// <summary>
///     String extension methods
/// </summary>
public static class StringExtensions
{
	private static readonly string[] Numbers =
		{ "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

	/// <summary>
	///     Reverses the String
	/// </summary>
	/// <param name="str">String to reverse</param>
	/// <returns>Reversed String</returns>
	public static string Reverse(this string str)
	{
		var chars = new char[str.Length];
		for (int i = 0, j = str.Length - 1; i <= j; i++, j--)
		{
			chars[i] = str[j];
			chars[j] = str[i];
		}

		return new string(chars);
	}

	/// <summary>
	///     If the given string is not null or a whitespace
	/// </summary>
	/// <param name="str">Input string</param>
	/// <returns>If this string is not null or a whitespace</returns>
	public static bool IsNotNullOrWhitespace(this string str)
		=> !string.IsNullOrWhiteSpace(str);

	/// <summary>
	///     Parses an string to an integer including written out numbers
	/// </summary>
	/// <param name="str">Input string</param>
	/// <returns>parsed integer</returns>
	/// <exception cref="FormatException">If the string can't be parsed to an integer</exception>
	public static int ToInteger(this string str)
	{
		var normalized = str.Normalize(NormalizationForm.FormKC);

		if (int.TryParse(normalized, out var number)) return number;

		number = Array.IndexOf(Numbers, str.ToLower());

		if (number != -1) return number;

		throw new FormatException($"{str} isn't a number");
	}

	/// <summary>
	///     Converts a string to a decimal including written out numbers
	/// </summary>
	/// <param name="str">Input String</param>
	/// <returns>parsed decimal</returns>
	/// <exception cref="FormatException">If the string can't be parsed to an decimal</exception>
	public static decimal ToDecimal(this string str)
	{
		var normalized = str.Normalize(NormalizationForm.FormKC);

		if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
			return number;

		throw new FormatException($"{str} isn't a number");
	}

	/// <summary>
	///     Normalises the given Release string
	/// </summary>
	/// <param name="str">Input string</param>
	/// <returns></returns>
	public static string NormalizeReleaseTitle(this string str)
		=> str.Replace('.', ' ')
			.Replace('_', ' ')
			.Trim();
}
