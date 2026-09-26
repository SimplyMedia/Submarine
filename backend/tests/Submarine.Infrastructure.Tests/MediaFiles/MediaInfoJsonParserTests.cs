using Shouldly;
using Submarine.Infrastructure.MediaFiles;
using Xunit;

namespace Submarine.Infrastructure.Tests.MediaFiles;

public sealed class MediaInfoJsonParserTests
{
	[Fact]
	public void Parse_ShouldExtractVideoAndAudioBasics()
	{
		const string json = """
			{
				"streams": [
					{ "codec_type": "video", "codec_name": "h264", "width": 1920, "height": 1080, "pix_fmt": "yuv420p" },
					{ "codec_type": "audio", "codec_name": "eac3", "channels": 6, "tags": { "language": "eng" } },
					{ "codec_type": "subtitle", "tags": { "language": "fre" } }
				],
				"format": { "duration": "3600.5" }
			}
			""";

		var info = MediaInfoJsonParser.Parse(json)!;

		info.VideoCodec.ShouldBe("h264");
		info.Width.ShouldBe(1920);
		info.Height.ShouldBe(1080);
		info.AudioCodec.ShouldBe("eac3");
		info.AudioChannels.ShouldBe(6);
		info.AudioLanguages.ShouldBe(["eng"]);
		info.SubtitleLanguages.ShouldBe(["fre"]);
		info.Runtime.ShouldBe(60);
		info.VideoBitDepth.ShouldBe(8);
		info.VideoDynamicRange.ShouldBeNull();
	}

	[Fact]
	public void Parse_ShouldDetectHdr10_FromColorTransfer()
	{
		const string json = """
			{ "streams": [ { "codec_type": "video", "codec_name": "hevc", "color_transfer": "smpte2084" } ] }
			""";

		MediaInfoJsonParser.Parse(json)!.VideoDynamicRange.ShouldBe("HDR10");
	}

	[Fact]
	public void Parse_ShouldDetectHlg_FromColorTransfer()
	{
		const string json = """
			{ "streams": [ { "codec_type": "video", "codec_name": "hevc", "color_transfer": "arib-std-b67" } ] }
			""";

		MediaInfoJsonParser.Parse(json)!.VideoDynamicRange.ShouldBe("HLG");
	}

	[Fact]
	public void Parse_ShouldDetectDolbyVision_FromSideData()
	{
		const string json = """
			{
				"streams": [
					{
						"codec_type": "video",
						"codec_name": "hevc",
						"color_transfer": "smpte2084",
						"side_data_list": [ { "side_data_type": "DOVI configuration record" } ]
					}
				]
			}
			""";

		MediaInfoJsonParser.Parse(json)!.VideoDynamicRange.ShouldBe("DV");
	}

	[Fact]
	public void Parse_ShouldDetectHdr10Plus_FromSideData()
	{
		const string json = """
			{
				"streams": [
					{
						"codec_type": "video",
						"codec_name": "hevc",
						"color_transfer": "smpte2084",
						"side_data_list": [ { "side_data_type": "HDR10+ metadata" } ]
					}
				]
			}
			""";

		MediaInfoJsonParser.Parse(json)!.VideoDynamicRange.ShouldBe("HDR10+");
	}

	[Fact]
	public void Parse_ShouldReadBitDepth_FromBitsPerRawSample()
	{
		const string json = """
			{ "streams": [ { "codec_type": "video", "codec_name": "hevc", "bits_per_raw_sample": "10" } ] }
			""";

		MediaInfoJsonParser.Parse(json)!.VideoBitDepth.ShouldBe(10);
	}

	[Fact]
	public void Parse_ShouldReturnNull_WhenNoStreamsProperty()
		=> MediaInfoJsonParser.Parse("{}").ShouldBeNull();

	[Fact]
	public void Parse_ShouldCombineMultipleAudioAndSubtitleLanguages()
	{
		const string json = """
			{
				"streams": [
					{ "codec_type": "video", "codec_name": "h264" },
					{ "codec_type": "audio", "codec_name": "aac", "tags": { "language": "eng" } },
					{ "codec_type": "audio", "codec_name": "aac", "tags": { "language": "fre" } },
					{ "codec_type": "subtitle", "tags": { "language": "eng" } },
					{ "codec_type": "subtitle", "tags": { "language": "spa" } }
				]
			}
			""";

		var info = MediaInfoJsonParser.Parse(json)!;

		info.AudioLanguages.ShouldBe(["eng", "fre"]);
		info.SubtitleLanguages.ShouldBe(["eng", "spa"]);
	}
}
