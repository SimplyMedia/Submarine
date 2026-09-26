using System.Text.RegularExpressions;

namespace Submarine.Core.Library;

/// <summary>
///     Normalizes titles for sorting and matching.
/// </summary>
public static partial class TitleNormalizer
{
	[GeneratedRegex("[^a-z0-9]")]
	private static partial Regex NonAlphanumeric();

	/// <summary>
	///     Lowercase alphanumeric only form of a title used for parsing and matching.
	/// </summary>
	public static string CleanTitle(string title)
		=> NonAlphanumeric().Replace(title.ToLowerInvariant(), string.Empty);

	/// <summary>
	///     Title with leading articles removed and lowercased, for sorting.
	/// </summary>
	public static string SortTitle(string title)
	{
		var cleaned = title.Trim().ToLowerInvariant();
		foreach (var article in (string[])["the ", "a ", "an "])
		{
			if (cleaned.StartsWith(article, StringComparison.Ordinal))
			{
				return cleaned[article.Length..];
			}
		}

		return cleaned;
	}
}
