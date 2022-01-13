using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser.Release;

public class TorrentReleaseParserService : IParser<TorrentRelease>
{
	private static readonly RegexReplace CleanTorrentSuffixRegex = new(@"\[(?:ettv|rartv|rarbg|cttv|publichd|TGx)\]$",
		string.Empty,
		RegexOptions.IgnoreCase | RegexOptions.Compiled);


	private readonly ILogger<TorrentReleaseParserService> _logger;

	private readonly IParser<BaseRelease> _releaseParserService;

	public TorrentReleaseParserService(
		ILogger<TorrentReleaseParserService> logger,
		IParser<BaseRelease> releaseParserService)
	{
		_logger = logger;
		_releaseParserService = releaseParserService;
	}

	public TorrentRelease Parse(string input)
	{
		_logger.LogDebug("Starting parse of {Input} with Bittorrent standards", input);

		input = CleanTorrentSuffixRegex.Replace(input);

		var parsed = _releaseParserService.Parse(input);

		return parsed.ToTorrent();
	}
}
