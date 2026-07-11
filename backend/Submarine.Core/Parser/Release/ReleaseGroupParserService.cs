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
		@"-(?<releasegroup>[a-z0-9]+(?!.+?(?:480p|720p|1080p|2160p)))(?<!.*?WEB-DL|Blu-Ray|480p|720p|1080p|2160p|DTS-HD|DTS-X|DTS-MA|DTS-ES)(?:\b|[-._ ]|$)|[-._ ]\[(?<releasegroup>[a-z0-9]+)\]$",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex AnimeReleaseGroupRegex = new(@"^(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
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
