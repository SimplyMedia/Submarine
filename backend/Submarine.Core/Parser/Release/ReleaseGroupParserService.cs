using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Submarine.Core.Parser.Release;

/// <summary>
///     Services to parse the Release Group of a release
/// </summary>
public class ReleaseGroupParserService : IParser<string?>
{
	private static readonly Regex ReleaseGroupRegex = new(
		@"-(?<releasegroup>[a-z0-9]+(?<part2>-[a-z0-9]+)?(?!.+?(?:480p|576p|720p|1080p|2160p)))(?<!(?:WEB-(DL|Rip)|Blu-Ray|480p|576p|720p|1080p|2160p|DTS-HD|DTS-X|DTS-MA|DTS-ES|-ES|-EN|-CAT|-ENG|-JAP|-GER|-FRA|-FRE|-ITA|-HDRip|\d{1,2}-bit|[ ._]\d{4}-\d{2}|-\d{2}|tmdb(id)?-(?<tmdbid>\d+)|(?<imdbid>tt\d{7,8}))(?:\k<part2>)?)(?:\b|[-._ ]|$)|[-._ ]\[(?<releasegroup>[a-z0-9]+)\]$",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex AnimeReleaseGroupRegex = new(@"^(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Handle Exception Release Groups that don't follow -RlsGrp; Manual List
	// name only...be very careful with this last; high chance of false positives
	private static readonly Regex ExceptionReleaseGroupRegexExact = new(
		@"\b(?<releasegroup>KRaLiMaRKo|E\.N\.D|D\-Z0N3|Koten_Gars|BluDragon|ZØNEHD|HQMUX|VARYG|YIFY|YTS(.(MX|LT|AG))?|TMd|Eml HDTeam|LMain|DarQ|BEN THE MEN|TAoE|QxR|Fight-BB|KCRT|126811)\b",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// groups whose releases end with RlsGroup) or RlsGroup]
	private static readonly Regex ExceptionReleaseGroupRegex = new(
		@"(?<=[._ \[])(?<releasegroup>(Silence|afm72|Panda|Ghost|MONOLITH|Tigole|Joy|ImE|UTR|t3nzin|Anime Time|Project Angel|Hakata Ramen|HONE|GiLG|Vyndros|SEV|Garshasp|Kappa|Natty|RCVR|SAMPA|YOGI|r00t|EDGE2020|RZeroX|FreetheFish|Anna|Bandi|Qman|theincognito|HDO|DusIctv|DHD|CtrlHD|-ZR-|ADC|XZVN|RH|Kametsu)(?=\]|\)))",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex InvalidReleaseGroupRegex =
		new(@"^([se]\d+|[0-9a-f]{8})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly ILogger<ReleaseGroupParserService> _logger;

	/// <summary>
	///     Creates a new <see cref="ReleaseGroupParserService" />
	/// </summary>
	/// <param name="logger">The Logger of this <see cref="ReleaseGroupParserService" /></param>
	public ReleaseGroupParserService(ILogger<ReleaseGroupParserService> logger)
		=> _logger = logger;

	/// <summary>
	///     Parses the Release Group of a release
	/// </summary>
	/// <param name="input">The Release</param>
	/// <returns>The release group, if any</returns>
	public string? Parse(string input)
	{
		_logger.LogDebug("Trying to parse Release Group for {Input}", input);

		var animeMatch = AnimeReleaseGroupRegex.Match(input);

		if (animeMatch.Success)
		{
			var subgroup = animeMatch.Groups["subgroup"].Value;
			_logger.LogDebug("{Input} matched subgroup {SubGroup} with AnimeReleaseGroupRegex", input, subgroup);
			return subgroup;
		}

		var exceptionExactMatches = ExceptionReleaseGroupRegexExact.Matches(input);

		if (exceptionExactMatches.Count != 0)
		{
			var exactGroup = exceptionExactMatches.Last().Groups["releasegroup"].Value;
			_logger.LogDebug("{Input} matched release group \"{Group}\" with ExceptionReleaseGroupRegexExact", input,
				exactGroup);
			return exactGroup;
		}

		var exceptionMatches = ExceptionReleaseGroupRegex.Matches(input);

		if (exceptionMatches.Count != 0)
		{
			var exceptionGroup = exceptionMatches.Last().Groups["releasegroup"].Value;
			_logger.LogDebug("{Input} matched release group \"{Group}\" with ExceptionReleaseGroupRegex", input,
				exceptionGroup);
			return exceptionGroup;
		}

		var matches = ReleaseGroupRegex.Matches(input);

		if (matches.Count == 0)
		{
			_logger.LogDebug("{Input} didn't match ReleaseGroupRegex", input);
			return null;
		}

		var group = matches.Last().Groups["releasegroup"].Value;

		if (int.TryParse(group, out _))
		{
			_logger.LogDebug(
				"{Input} matched release group \"{Group}\" with ReleaseGroupRegex but group parses to an integer, so we assume its invalid",
				input, group);
			return null;
		}

		if (InvalidReleaseGroupRegex.IsMatch(group))
		{
			_logger.LogDebug(
				"{Input} matched release group \"{Group}\" with ReleaseGroupRegex but also matched InvalidReleaseGroupRegex, so its invalid",
				input, group);
			return null;
		}

		_logger.LogDebug("{Input} matched release group \"{Group}\" with ReleaseGroupRegex", input, group);

		return group;
	}
}
