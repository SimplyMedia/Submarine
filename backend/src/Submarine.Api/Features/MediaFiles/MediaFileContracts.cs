using Submarine.Core.Entities;

namespace Submarine.Api.Features.MediaFiles;

/// <summary>Technical media info of a file.</summary>
/// <param name="VideoCodec">Video codec.</param>
/// <param name="AudioCodec">Audio codec.</param>
/// <param name="AudioChannels">Number of audio channels.</param>
/// <param name="VideoDynamicRange">SDR, HDR10, HDR10+, DV or HLG.</param>
/// <param name="Width">Video width in pixels.</param>
/// <param name="Height">Video height in pixels.</param>
/// <param name="Runtime">Runtime in minutes.</param>
/// <param name="VideoBitDepth">Video bit depth, if known.</param>
/// <param name="AudioLanguages">Languages of the audio tracks, if known.</param>
/// <param name="SubtitleLanguages">Languages of the subtitle tracks, if known.</param>
public sealed record MediaInfoDto(
	string? VideoCodec,
	string? AudioCodec,
	double? AudioChannels,
	string? VideoDynamicRange,
	int? Width,
	int? Height,
	int? Runtime,
	int? VideoBitDepth,
	IReadOnlyList<string>? AudioLanguages,
	IReadOnlyList<string>? SubtitleLanguages);

/// <summary>
///     Maps <see cref="MediaInfoModel" /> to its API resource.
/// </summary>
public static class MediaInfoMapper
{
	/// <summary>Maps a media info model to its DTO, null-safe.</summary>
	public static MediaInfoDto? ToDto(MediaInfoModel? model)
		=> model is null
			? null
			: new MediaInfoDto(
				model.VideoCodec,
				model.AudioCodec,
				model.AudioChannels,
				model.VideoDynamicRange,
				model.Width,
				model.Height,
				model.Runtime,
				model.VideoBitDepth,
				model.AudioLanguages,
				model.SubtitleLanguages);
}
