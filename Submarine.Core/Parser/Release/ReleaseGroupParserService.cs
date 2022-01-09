using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Submarine.Core.Parser.Release;

public class ReleaseGroupParserService : IParser<string?>
{
	private static readonly Regex ReleaseGroupRegex = new(
		@"-(?<releasegroup>[a-z0-9]+(?!.+?(?:480p|720p|1080p|2160p)))(?<!.*?WEB-DL|Blu-Ray|480p|720p|1080p|2160p|DTS-HD|DTS-X|DTS-MA|DTS-ES)(?:\b|[-._ ]|$)|[-._ ]\[(?<releasegroup>[a-z0-9]+)\]$",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex AnimeReleaseGroupRegex = new(@"^(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex InvalidReleaseGroupRegex =
		new(@"^([se]\d+|[0-9a-f]{8})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly ILogger<ReleaseGroupParserService> _logger;

	public ReleaseGroupParserService(ILogger<ReleaseGroupParserService> logger)
		=> _logger = logger;

	public string? Parse(string input)
	{
		_logger.LogDebug("Trying to parse language for {Input}", input);

		var animeMatch = AnimeReleaseGroupRegex.Match(input);

		if (animeMatch.Success) return animeMatch.Groups["subgroup"].Value;

		var matches = ReleaseGroupRegex.Matches(input);

		if (matches.Count == 0) return null;
		var group = matches.Last().Groups["releasegroup"].Value;

		if (int.TryParse(group, out _)) return null;

		return InvalidReleaseGroupRegex.IsMatch(group) ? null : group;
	}
}
