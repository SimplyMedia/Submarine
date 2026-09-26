using System.Globalization;
using System.Text.Json;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.MediaFiles;

/// <summary>
///     Parses the JSON ffprobe emits with -show_format -show_streams into a <see cref="MediaInfoModel" />.
/// </summary>
internal static class MediaInfoJsonParser
{
	/// <summary>
	///     Parse ffprobe output. Returns null when the JSON carries no streams array.
	/// </summary>
	public static MediaInfoModel? Parse(string json)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		JsonElement? video = null;
		string? audioCodec = null;
		double? audioChannels = null;
		var audioLanguages = new List<string>();
		var subtitleLanguages = new List<string>();

		foreach (var stream in streams.EnumerateArray())
		{
			var codecType = GetString(stream, "codec_type");
			switch (codecType)
			{
				case "video":
					video ??= stream;
					break;
				case "audio":
					if (audioCodec is null)
					{
						audioCodec = GetString(stream, "codec_name");
						audioChannels = stream.TryGetProperty("channels", out var channels) && channels.ValueKind == JsonValueKind.Number
							? channels.GetDouble()
							: null;
					}

					if (LanguageOf(stream) is { } audioLanguage)
					{
						audioLanguages.Add(audioLanguage);
					}

					break;
				case "subtitle":
					if (LanguageOf(stream) is { } subtitleLanguage)
					{
						subtitleLanguages.Add(subtitleLanguage);
					}

					break;
			}
		}

		string? videoCodec = null;
		int? width = null;
		int? height = null;
		int? bitDepth = null;
		string? dynamicRange = null;

		if (video is { } videoStream)
		{
			videoCodec = GetString(videoStream, "codec_name");
			width = GetInt(videoStream, "width");
			height = GetInt(videoStream, "height");
			bitDepth = ReadBitDepth(videoStream);
			dynamicRange = DetectDynamicRange(videoStream);
		}

		int? runtime = null;
		if (root.TryGetProperty("format", out var format)
			&& format.TryGetProperty("duration", out var durationProperty)
			&& double.TryParse(RawText(durationProperty), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
		{
			runtime = (int)Math.Round(seconds / 60);
		}

		return new MediaInfoModel(
			videoCodec,
			audioCodec,
			audioChannels,
			dynamicRange,
			width,
			height,
			runtime,
			bitDepth,
			audioLanguages.Count > 0 ? audioLanguages : null,
			subtitleLanguages.Count > 0 ? subtitleLanguages : null);
	}

	private static string? LanguageOf(JsonElement stream)
		=> stream.TryGetProperty("tags", out var tags) ? GetString(tags, "language") : null;

	private static int? ReadBitDepth(JsonElement video)
	{
		if (video.TryGetProperty("bits_per_raw_sample", out var raw) && int.TryParse(RawText(raw), out var bits))
		{
			return bits;
		}

		var pixelFormat = GetString(video, "pix_fmt");
		return pixelFormat is not null && pixelFormat.Contains("10", StringComparison.Ordinal) ? 10 : 8;
	}

	private static string? DetectDynamicRange(JsonElement video)
	{
		if (HasSideData(video, "dovi"))
		{
			return "DV";
		}

		if (HasSideData(video, "hdr10+") || HasSideData(video, "2094"))
		{
			return "HDR10+";
		}

		return GetString(video, "color_transfer") switch
		{
			"smpte2084" => "HDR10",
			"arib-std-b67" => "HLG",
			_ => null
		};
	}

	private static bool HasSideData(JsonElement video, string marker)
		=> video.TryGetProperty("side_data_list", out var sideDataList)
		   && sideDataList.ValueKind == JsonValueKind.Array
		   && sideDataList.EnumerateArray()
			   .Any(item => GetString(item, "side_data_type")?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true);

	private static string? GetString(JsonElement element, string property)
		=> element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

	private static int? GetInt(JsonElement element, string property)
		=> element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

	private static string? RawText(JsonElement element)
		=> element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Number => element.GetRawText(),
			_ => null
		};
}
