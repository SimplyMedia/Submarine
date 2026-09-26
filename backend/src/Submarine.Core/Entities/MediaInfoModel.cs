namespace Submarine.Core.Entities;

/// <summary>
///     Technical details of a media file, extracted with ffprobe. Persisted as JSON.
/// </summary>
/// <param name="VideoCodec">Video codec.</param>
/// <param name="AudioCodec">Audio codec.</param>
/// <param name="AudioChannels">Number of audio channels.</param>
/// <param name="VideoDynamicRange">SDR or HDR flavour.</param>
/// <param name="Width">Video width in pixels.</param>
/// <param name="Height">Video height in pixels.</param>
/// <param name="Runtime">Runtime in minutes.</param>
/// <param name="VideoBitDepth">Video bit depth in bits, if known.</param>
/// <param name="AudioLanguages">Languages of the audio tracks, if known.</param>
/// <param name="SubtitleLanguages">Languages of the subtitle tracks, if known.</param>
public sealed record MediaInfoModel(
	string? VideoCodec,
	string? AudioCodec,
	double? AudioChannels,
	string? VideoDynamicRange,
	int? Width,
	int? Height,
	int? Runtime,
	int? VideoBitDepth = null,
	IReadOnlyList<string>? AudioLanguages = null,
	IReadOnlyList<string>? SubtitleLanguages = null);
