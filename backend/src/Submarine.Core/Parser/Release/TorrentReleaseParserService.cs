using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Util.RegEx;
using Submarine.Core.Validator;

namespace Submarine.Core.Parser.Release;

/// <summary>
///     Service to parse a release with Bittorrent standards
/// </summary>
public class TorrentReleaseParserService : IParser<TorrentRelease>
{
	private static readonly RegexReplace CleanTorrentSuffixRegex = new(@"\[(?:ettv|rartv|rarbg|cttv|publichd|TGx)\]$",
		string.Empty,
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Matches Freeleech promotions, e.g. "[Freeleech]", "FreeLeech", or the "FL" shorthand
	private static readonly Regex FreeleechRegex = new(@"\b(?:FREE ?LEECH|FL)\b",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Matches Halfleech promotions, e.g. "[Halfleech]", "Half Leech"
	private static readonly Regex HalfleechRegex = new(@"\bHALF ?LEECH\b",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Matches Neutralleech promotions, e.g. "[Neutralleech]", "Neutral Leech"
	private static readonly Regex NeutralleechRegex = new(@"\bNEUTRAL ?LEECH\b",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Matches Double Upload promotions, e.g. "[Double Upload]" or the "DU" shorthand
	private static readonly Regex DoubleUploadRegex = new(@"\b(?:DOUBLE ?UPLOAD|DU)\b",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Matches releases marked as Internal by the tracker, e.g. "Movie.2020.iNTERNAL.1080p..."
	private static readonly Regex InternalRegex = new(@"\bINTERNAL\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Matches Scene releases, e.g. "Movie.2020.1080p.BluRay.x264-SCENE"
	private static readonly Regex SceneRegex = new(@"\bSCENE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly ILogger<TorrentReleaseParserService> _logger;

	private readonly IParser<BaseRelease> _releaseParserService;

	private readonly TorrentReleaseValidatorService _torrentReleaseValidatorService;

	/// <summary>
	///     Creates a new <see cref="TorrentReleaseParserService" />
	/// </summary>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}" /></param>
	/// <param name="torrentReleaseValidatorService">The <see cref="TorrentReleaseValidatorService" /></param>
	/// <param name="releaseParserService">The <see cref="ReleaseParserService" /></param>
	public TorrentReleaseParserService(
		ILogger<TorrentReleaseParserService> logger,
		TorrentReleaseValidatorService torrentReleaseValidatorService,
		IParser<BaseRelease> releaseParserService)
	{
		_logger = logger;
		_torrentReleaseValidatorService = torrentReleaseValidatorService;
		_releaseParserService = releaseParserService;
	}

	/// <summary>
	///     Parses a release with Bittorrent standards
	/// </summary>
	/// <param name="input">Release Title</param>
	/// <returns>A <see cref="TorrentRelease" /></returns>
	public TorrentRelease Parse(string input)
	{
		_logger.LogDebug("Starting parse of {Input} with Bittorrent standards", input);

		_torrentReleaseValidatorService.Validate(input);

		var flags = ParseFlags(input);

		input = CleanTorrentSuffixRegex.Replace(input);

		var parsed = _releaseParserService.Parse(input);

		return parsed.ToTorrent() with { Flags = flags };
	}

	private static TorrentReleaseFlags ParseFlags(string input)
	{
		var flags = TorrentReleaseFlags.NONE;

		if (FreeleechRegex.IsMatch(input)) flags |= TorrentReleaseFlags.FREELEECH;
		if (HalfleechRegex.IsMatch(input)) flags |= TorrentReleaseFlags.HALFLEECH;
		if (NeutralleechRegex.IsMatch(input)) flags |= TorrentReleaseFlags.NEUTRALLEECH;
		if (DoubleUploadRegex.IsMatch(input)) flags |= TorrentReleaseFlags.DOUBLE_UPLOAD;
		if (InternalRegex.IsMatch(input)) flags |= TorrentReleaseFlags.INTERNAL;
		if (SceneRegex.IsMatch(input)) flags |= TorrentReleaseFlags.SCENE;

		return flags;
	}
}
