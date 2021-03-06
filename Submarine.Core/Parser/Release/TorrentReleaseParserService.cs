using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser.Release;

public class TorrentReleaseParserService : IParser<TorrentRelease>
{
	private static readonly RegexReplace CleanTorrentSuffixRegex = new(@"\[(?:ettv|rartv|rarbg|cttv|publichd|TGx)\]$",
		string.Empty,
		RegexOptions.IgnoreCase | RegexOptions.Compiled);


	private readonly ILogger<TorrentReleaseParserService> _logger;

	private readonly ReleaseParserService _releaseParserService;

	public TorrentReleaseParserService(
		ILogger<TorrentReleaseParserService> logger,
		ReleaseParserService releaseParserService)
	{
		_logger = logger;
		_releaseParserService = releaseParserService;
	}

	public TorrentRelease Parse(string input)
	{
		_logger.LogDebug("Starting Parse of {Input}", input);

		input = CleanTorrentSuffixRegex.Replace(input);

		var parsed = _releaseParserService.Parse(input);

		return parsed.ToTorrent();
	}
}
