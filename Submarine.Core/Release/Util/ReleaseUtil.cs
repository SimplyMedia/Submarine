using System.Linq;
using System.Text.RegularExpressions;
using Submarine.Core.Util.RegEx;
using static Submarine.Core.MediaFile.MediaFile;

namespace Submarine.Core.Release.Util;

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

	public static string RemoveFileExtension(string title)
		=> FileExtensionRegex.Replace(title);
}
