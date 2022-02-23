namespace Submarine.Core.Util.Extensions;

/// <summary>
///     String extension methods
/// </summary>
public static class StringExtensions
{
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
	///		Normalises the given Release string
	/// </summary>
	/// <param name="str">Input string</param>
	/// <returns></returns>
	public static string NormalizeReleaseTitle(this string str)
		=> str.Replace('.', ' ')
			.Replace('_', ' ')
			.Trim();
}
