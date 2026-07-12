using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Release.Exceptions;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Validator;
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
			new ReleaseGroupParserService(new XunitLogger<ReleaseGroupParserService>(output)),
			new StaticQualityOverrideSource());

		_instance = new TorrentReleaseParserService(new XunitLogger<TorrentReleaseParserService>(output),
			new TorrentReleaseValidatorService(new XunitLogger<TorrentReleaseValidatorService>(output)),
			releaseParserService);
	}

	[Theory]
	[InlineData("Movie.Title.2020.1080p.KORSUB.HDRip.x264-GROUP")]
	[InlineData("Movie.Title.2020.1080p.HC.WEBRip.x264-GROUP")]
	public void Parse_ShouldKeepHardcodedSubs_WhenConvertedToTorrent(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.True(parsed.HardcodedSubs);
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

	[Theory]
	[InlineData("3fc4b8a1d2e6f7098a1b2c3d4e5f6071")] // ^[0-9a-zA-Z]{32}
	[InlineData("abcdef0123456789abcdef01")] // ^[a-z0-9]{24}$
	[InlineData("ABCDEFGHIJK123")] // ^[A-Z]{11}\d{3}$
	[InlineData("abcdefghijkl123")] // ^[a-z]{12}\d{3}$
	[InlineData("Backup_12345S01-02")] // ^Backup_\d{5,}S\d{2}-\d{2}$
	[InlineData("123")] // ^123$
	[InlineData("abc")] // ^abc$
	[InlineData("abc.xyz")] // ^abc[-_. ]xyz
	[InlineData("b00bs")] // ^b00bs$
	[InlineData("170424_26")] // ^\d{6}_\d{2}$
	[InlineData("abcdefghij0123456789ABCDEFGHIJ")] // ^[0-9a-zA-Z]{30}
	[InlineData("abcdefghij0123456789ABCDEF")] // ^[0-9a-zA-Z]{26}
	[InlineData("abcdefghij0123456789ABCDEFGHIJ0123456789")] // ^[0-9a-zA-Z]{39}
	[InlineData("abcdefghij0123456789ABCD")] // ^[0-9a-zA-Z]{24}
	public void Parse_ShouldRejectHashedRelease(string input)
		=> Assert.Throws<InvalidReleaseException>(() => _instance.Parse(input));

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC-GROUP")]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP")]
	public void Parse_ShouldNotRejectLegitRelease(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.NotNull(parsed);
	}

	private static void AssertHasFlag(TorrentRelease parsed, TorrentReleaseFlags flag)
		=> Assert.Equal(flag, parsed.Flags & flag);
}
