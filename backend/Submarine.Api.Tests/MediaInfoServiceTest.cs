using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Services;
using Xunit;

namespace Submarine.Api.Tests;

public class MediaInfoServiceTest
{
	private const string FfprobeJson = """
		{
			"streams": [
				{ "codec_type": "video", "codec_name": "hevc", "width": 3840, "height": 2160, "color_transfer": "smpte2084" },
				{ "codec_type": "audio", "codec_name": "eac3", "channels": 6, "tags": { "language": "eng" } },
				{ "codec_type": "audio", "codec_name": "aac", "channels": 2, "tags": { "language": "jpn" } },
				{ "codec_type": "audio", "codec_name": "aac", "channels": 2, "tags": { "language": "eng" } },
				{ "codec_type": "subtitle", "codec_name": "subrip", "tags": { "language": "eng" } },
				{ "codec_type": "subtitle", "codec_name": "subrip", "tags": { "language": "ger" } }
			],
			"format": { "duration": "1234.567000" }
		}
		""";

	[Fact]
	public async Task ProbeAsync_ShouldParseStreamsAndFormat_WhenFfprobeReturnsJson()
	{
		var service = new FakeMediaInfoService(FfprobeJson);

		var info = await service.ProbeAsync("video.mkv");

		Assert.NotNull(info);
		Assert.Equal("hevc", info.VideoCodec);
		Assert.Equal(3840, info.Width);
		Assert.Equal(2160, info.Height);
		Assert.Equal("HDR10", info.VideoDynamicRange);
		Assert.Equal("eac3", info.AudioCodec);
		Assert.Equal(6d, info.AudioChannels);
		Assert.Equal(new List<string> { "eng", "jpn" }, info.AudioLanguages);
		Assert.Equal(new List<string> { "eng", "ger" }, info.SubtitleLanguages);
		Assert.Equal(1235, info.RuntimeSeconds);
	}

	[Theory]
	[InlineData("arib-std-b67", "HLG")]
	[InlineData("bt709", null)]
	[InlineData(null, null)]
	public async Task ProbeAsync_ShouldMapDynamicRange_WhenColorTransferVaries(string? colorTransfer,
		string? expected)
	{
		var transfer = colorTransfer == null ? "" : $", \"color_transfer\": \"{colorTransfer}\"";
		var json = $"{{ \"streams\": [ {{ \"codec_type\": \"video\", \"codec_name\": \"h264\"{transfer} }} ] }}";
		var service = new FakeMediaInfoService(json);

		var info = await service.ProbeAsync("video.mkv");

		Assert.NotNull(info);
		Assert.Equal(expected, info.VideoDynamicRange);
	}

	[Fact]
	public async Task ProbeAsync_ShouldReturnNull_WhenFfprobeBinaryIsMissing()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["FFprobe:Path"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
			})
			.Build();

		var service = new MediaInfoService(configuration, NullLogger<MediaInfoService>.Instance);

		Assert.Null(await service.ProbeAsync("video.mkv"));
	}
}
