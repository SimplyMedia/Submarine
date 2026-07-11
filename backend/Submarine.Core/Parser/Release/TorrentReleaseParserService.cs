using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser.Release;

/// <summary>
///     Service to parse a release with Bittorrent standards
/// </summary>
public class TorrentReleaseParserService : IParser<TorrentRelease>
{
	private static readonly RegexReplace CleanTorrentSuffixRegex = new(@"\[(?:ettv|rartv|rarbg|cttv|publichd|TGx)\]$",
		string.Empty,
		RegexOptions.IgnoreCase | RegexOptions.Compiled);


	private readonly ILogger<TorrentReleaseParserService> _logger;

	private readonly IParser<BaseRelease> _releaseParserService;

	/// <summary>
	///     Creates a new <see cref="TorrentReleaseParserService" />
	/// </summary>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}" /></param>
	/// <param name="releaseParserService">The <see cref="ReleaseParserService" /></param>
	public TorrentReleaseParserService(
		ILogger<TorrentReleaseParserService> logger,
		IParser<BaseRelease> releaseParserService)
	{
		_logger = logger;
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

		input = CleanTorrentSuffixRegex.Replace(input);

		var parsed = _releaseParserService.Parse(input);

		return parsed.ToTorrent();
	}
}
