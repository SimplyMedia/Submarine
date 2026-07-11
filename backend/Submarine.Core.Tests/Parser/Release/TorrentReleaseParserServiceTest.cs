using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Release.Torrent;
using Xunit;

namespace Submarine.Core.Tests.Parser.Release;

public class TorrentReleaseParserServiceTest
{
	private readonly IParser<TorrentRelease> _instance;

	public TorrentReleaseParserServiceTest(ITestOutputHelper output)
	{
		var releaseParserService = new ReleaseParserService(new XunitLogger<ReleaseParserService>(output),
			new LanguageParserService(new XunitLogger<LanguageParserService>(output)),
			new StreamingProviderParserService(new XunitLogger<StreamingProviderParserService>(output)),
			new QualityParserService(new XunitLogger<QualityParserService>(output)),
			new ReleaseGroupParserService(new XunitLogger<ReleaseGroupParserService>(output)));

		_instance = new TorrentReleaseParserService(new XunitLogger<TorrentReleaseParserService>(output),
			releaseParserService);
	}

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [Freeleech]")]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [FL]")]
	public void Parse_ShouldSetFreeleechFlag_WhenTitleContainsFreeleech(string input)
	{
		var parsed = _instance.Parse(input);

		AssertHasFlag(parsed, TorrentReleaseFlags.FREELEECH);
	}

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [Halfleech]")]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [Half Leech]")]
	public void Parse_ShouldSetHalfleechFlag_WhenTitleContainsHalfleech(string input)
	{
		var parsed = _instance.Parse(input);

		AssertHasFlag(parsed, TorrentReleaseFlags.HALFLEECH);
	}

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [Neutralleech]")]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [Neutral Leech]")]
	public void Parse_ShouldSetNeutralleechFlag_WhenTitleContainsNeutralleech(string input)
	{
		var parsed = _instance.Parse(input);

		AssertHasFlag(parsed, TorrentReleaseFlags.NEUTRALLEECH);
	}

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [Double Upload]")]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP [DU]")]
	public void Parse_ShouldSetDoubleUploadFlag_WhenTitleContainsDoubleUpload(string input)
	{
		var parsed = _instance.Parse(input);

		AssertHasFlag(parsed, TorrentReleaseFlags.DOUBLE_UPLOAD);
	}

	[Theory]
	[InlineData("Movie.Title.1987.iNTERNAL.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP")]
	public void Parse_ShouldSetInternalFlag_WhenTitleContainsInternal(string input)
	{
		var parsed = _instance.Parse(input);

		AssertHasFlag(parsed, TorrentReleaseFlags.INTERNAL);
	}

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-SCENE")]
	public void Parse_ShouldSetSceneFlag_WhenTitleContainsScene(string input)
	{
		var parsed = _instance.Parse(input);

		AssertHasFlag(parsed, TorrentReleaseFlags.SCENE);
	}

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP")]
	public void Parse_ShouldSetNoFlags_WhenTitleContainsNoPromotion(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(TorrentReleaseFlags.NONE, parsed.Flags);
	}

	private static void AssertHasFlag(TorrentRelease parsed, TorrentReleaseFlags flag)
		=> Assert.Equal(flag, parsed.Flags & flag);
}
