using System.Collections.Generic;

namespace Submarine.Core.MediaFile;

/// <summary>
///     Technical media information extracted from a <see cref="EpisodeFile" /> or <see cref="MovieFile" />
/// </summary>
public record MediaInfo
{
	/// <summary>
	///     Video codec of the file, e.g. "h264" or "hevc"
	/// </summary>
	public string? VideoCodec { get; set; }

	/// <summary>
	///     Audio codec of the file, e.g. "aac" or "dts"
	/// </summary>
	public string? AudioCodec { get; set; }

	/// <summary>
	///     Number of audio channels, e.g. 5.1
	/// </summary>
	public double? AudioChannels { get; set; }

	/// <summary>
	///     Languages of the audio tracks contained in the file
	/// </summary>
	public List<string> AudioLanguages { get; set; } = new();

	/// <summary>
	///     Languages of the subtitle tracks contained in the file
	/// </summary>
	public List<string> SubtitleLanguages { get; set; } = new();

	/// <summary>
	///     Width of the video in pixels
	/// </summary>
	public int? Width { get; set; }

	/// <summary>
	///     Height of the video in pixels
	/// </summary>
	public int? Height { get; set; }

	/// <summary>
	///     Dynamic range of the video, e.g. "HDR10" or "Dolby Vision"
	/// </summary>
	public string? VideoDynamicRange { get; set; }

	/// <summary>
	///     Runtime of the file in seconds
	/// </summary>
	public int? RuntimeSeconds { get; set; }
}
