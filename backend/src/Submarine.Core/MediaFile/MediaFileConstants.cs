using System.Collections.Generic;
using System.Collections.Immutable;

namespace Submarine.Core.MediaFile;

/// <summary>
///     Constants related to MediaFiles
/// </summary>
public static class MediaFileConstants
{
	/// <summary>
	///     Extensions of media files
	/// </summary>
	public static readonly IImmutableSet<string> MediaFileExtensions = new HashSet<string>
	{
		".webm",
		".m4v",
		".3gp",
		".nsv",
		".ty",
		".strm",
		".rm",
		".rmvb",
		".m3u",
		".ifo",
		".mov",
		".qt",
		".divx",
		".xvid",
		".bivx",
		".nrg",
		".pva",
		".wmv",
		".asf",
		".asx",
		".ogm",
		".ogv",
		".m2v",
		".avi",
		".bin",
		".dat",
		".dvr-ms",
		".mpg",
		".mpeg",
		".mp4",
		".avc",
		".vp3",
		".svq3",
		".nuv",
		".viv",
		".dv",
		".fli",
		".flv",
		".wpl",
		".img",
		".iso",
		".vob",
		".mkv",
		".ts",
		".wtv",
		".m2ts"
	}.ToImmutableHashSet();
}
