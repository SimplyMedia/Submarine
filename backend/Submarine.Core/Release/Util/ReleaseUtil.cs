using System.Linq;
using System.Text.RegularExpressions;
using Submarine.Core.Util.RegEx;
using static Submarine.Core.MediaFile.MediaFileConstants;

namespace Submarine.Core.Release.Util;

/// <summary>
///     Utilities for Releases
/// </summary>
public static class ReleaseUtil
{
	private static readonly string[] UsenetExtensions = { ".par2", ".nzb" };

	private static readonly RegexReplace FileExtensionRegex = new(@"\.[a-z0-9]{2,4}$", m =>
	{
		var extension = m.Value.ToLower();
		if (MediaFileExtensions.Contains(extension) || UsenetExtensions.Contains(extension))
			return string.Empty;

		return m.Value;
	}, RegexOptions.IgnoreCase | RegexOptions.Compiled);

	/// <summary>
	///     Removes the file extension from a release title
	/// </summary>
	/// <param name="title">The release title</param>
	/// <returns>The replaced String</returns>
	public static string RemoveFileExtension(string title)
		=> FileExtensionRegex.Replace(title);
}
