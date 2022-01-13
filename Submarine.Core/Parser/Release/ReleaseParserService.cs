using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;
using Submarine.Core.Release.Util;
using Submarine.Core.Util.Extensions;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser.Release;

public class ReleaseParserService : IParser<BaseRelease>
{
	private static readonly RegexReplace WebsitePrefixRegex = new(
		@"^\[\s*[-a-z]+(\.[a-z]+)+\s*\][- ]*|^www\.[a-z]+\.(?:com|net|org)[ -]*",
		string.Empty,
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly RegexReplace WebsitePostfixRegex = new(@"\[\s*[-a-z]+(\.[a-z0-9]+)+\s*\]$",
		string.Empty,
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly RegexReplace[] PreSubstitutionRegex =
	{
		// Korean series without season number, replace with S01Exxx and remove airdate
		new(@"\.E(\d{2,4})\.\d{6}\.(.*-NEXT)$", ".S01E$1.$2", RegexOptions.Compiled),

		// Chinese LoliHouse/ZERO/Lilith-Raws releases don't use the expected brackets, normalize using brackets
		new(
			@"^\[(?<subgroup>[^\]]*?(?:LoliHouse|ZERO|Lilith-Raws)[^\]]*?)\](?<title>[^\[\]]+?)(?: - (?<episode>[0-9-]+)\s*|\[第?(?<episode>[0-9]+(?:-[0-9]+)?)话?(?:END|完)?\])\[",
			"[${subgroup}][${title}][${episode}][", RegexOptions.Compiled),

		// Most Chinese anime releases contain additional brackets/separators for chinese and non-chinese titles, remove junk and replace with normal anime pattern
		new(
			@"^\[(?<subgroup>[^\]]+)\](?:\s?★[^\[ -]+\s?)?\[?(?:(?<chinesetitle>[^\]]*?[\u4E00-\u9FCC][^\]]*?)(?:\]\[|\s*[_/·]\s*))?(?<title>[^\]]+?)\]?(?:\[\d{4}\])?\[第?(?<episode>[0-9]+(?:-[0-9]+)?)话?(?:END|完)?\]",
			"[${subgroup}] ${title} - ${episode} ", RegexOptions.Compiled),

		// Some Chinese anime releases contain both Chinese and English titles, remove the Chinese title and replace with normal anime pattern
		new(
			@"^\[(?<subgroup>[^\]]+)\](?:\s)(?:(?<chinesetitle>[^\]]*?[\u4E00-\u9FCC][^\]]*?)(?:\s/\s))(?<title>[^\]]+?)(?:[- ]+)(?<episode>[0-9]+(?:-[0-9]+)?)话?(?:END|完)?",
			"[${subgroup}] ${title} - ${episode} ", RegexOptions.Compiled)
	};

	private static readonly Regex[] ReportTitleRegex =
	{
		// Daily episode with year in series title and air time after date (Plex DVR format)
		new(
			@"^^(?<title>.+?\((?<titleyear>\d{4})\))[-_. ]+(?<airyear>19[4-9]\d|20\d\d)(?<sep>[-_]?)(?<airmonth>0\d|1[0-2])\k<sep>(?<airday>[0-2]\d|3[01])[-_. ]\d{2}[-_. ]\d{2}[-_. ]\d{2}",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Daily episodes without title (2018-10-12, 20181012) (Strict pattern to avoid false matches)
		new(@"^(?<airyear>19[6-9]\d|20\d\d)(?<sep>[-_]?)(?<airmonth>0\d|1[0-2])\k<sep>(?<airday>[0-2]\d|3[01])(?!\d)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Multi-Part episodes without a title (S01E05.S01E06)
		new(@"^(?:\W*S(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:e{1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Multi-Part episodes without a title (1x05.1x06)
		new(@"^(?:\W*(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:x{1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes without a title, Multi (S01E04E05, 1x04x05, etc)
		new(@"^(?:S?(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_]|[ex]){1,2}(?<episode>\d{2,3}(?!\d+))){2,})",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes without a title, Single (S01E05, 1x05)
		new(@"^(?:S?(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_ ]?[ex])(?<episode>\d{2,3}(?!\d+))))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title Episode Absolute Episode Number ([SubGroup] Series Title Episode 01)
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>.+?)[-_. ](?:Episode)(?:[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:_|-|\s|\.)*?(?<hash>\[.{8}\])?(?:$|\.)?",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title Absolute Episode Number + Season+Episode
		new(
			@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<absoluteepisode>\d{2,3}(\.\d{1,2})?))+(?:_|-|\s|\.)+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+)))+).*?(?<hash>[(\[]\w{8}[)\]])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title Season+Episode + Absolute Episode Number
		new(
			@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)(?:[-_\W](?<![()\[!]))+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+)))+)(?:(?:_|-|\s|\.)+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+|\-[a-z])))+.*?(?<hash>\[\w{8}\])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title Season+Episode
		new(
			@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)(?:[-_\W](?<![()\[!]))+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+)))+)(?:\s|\.).*?(?<hash>\[\w{8}\])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title with trailing number Absolute Episode Number - Batch separated with tilde
		new(
			@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?[^-]+?)(?:(?<![-_. ]|\b[0]\d+) - )[-_. ]?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+))\s?~\s?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+))(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>\[\w{8}\])?(?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title with season number in brackets Absolute Episode Number
		new(
			@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>[^-]+?)[_. ]+?\(Season[_. ](?<season>\d+)\)[-_. ]+?(?:[-_. ]?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>\[\w{8}\])?(?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title with trailing number Absolute Episode Number
		new(
			@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>[^-]+?)(?:(?<![-_. ]|\b[0]\d+) - )(?:[-_. ]?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>\[\w{8}\])?(?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title with trailing number Absolute Episode Number
		new(
			@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>[^-]+?)(?:(?<![-_. ]|\b[0]\d+)[_ ]+)(?:[-_. ]?(?<absoluteepisode>\d{3}(\.\d{1,2})?(?!\d+|-[a-z]+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>\[\w{8}\])?(?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title - Absolute Episode Number
		new(
			@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)(?:(?<!\b[0]\d+))(?:[. ]-[. ](?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-])))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>\[\w{8}\])?(?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title Absolute Episode Number - Absolute Episode Number (batches without full separator between title and absolute episode numbers)
		new(
			@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)(?:(?<!\b[0]\d+))(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-]))[. ]-[. ](?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-]))(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>\[\w{8}\])?(?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - [SubGroup] Title Absolute Episode Number
		new(
			@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)[-_. ]+\(?(?:[-_. ]?#?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|-[a-z]+)))+\)?(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>\[\w{8}\])?(?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Multi-episode Repeated (S01E05 - S01E06)
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:e|[-_. ]e){1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Multi-episode Repeated (1x05 - 1x06)
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:x{1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Single episodes with a title (S01E05, 1x05, etc) and trailing info in slashes
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+|(?:[ex]|\W[ex]|_|-){1,2}\d+))).+?(?:\[.+?\])(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Season EpisodeNumber + Absolute Episode Number [SubGroup]
		new(
			@"^(?<title>.+?)(?:[-_\W](?<![()\[!]))+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:[ex]|\W[ex]|-){1,2}(?<episode>(?<!\d+)\d{2}(?!\d+)))+)[-_. (]+?(?:[-_. ]?(?<absoluteepisode>(?<!\d+)\d{3}(\.\d{1,2})?(?!\d+|[pi])))+.+?\[(?<subgroup>.+?)\](?:$|\.mkv)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Multi-Episode with a title (S01E05E06, S01E05-06, S01E05 E06, etc) and trailing info in slashes
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+)))+).+?(?:\[.+?\])(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Absolute Episode Number [SubGroup] [Hash]? (Series Title Episode 99-100 [RlsGroup] [ABCD1234])
		new(
			@"^(?<title>.+?)[-_. ]Episode(?:[-_. ]+(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:.+?)\[(?<subgroup>.+?)\].*?(?<hash>\[\w{8}\])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Absolute Episode Number [SubGroup] [Hash]
		new(
			@"^(?<title>.+?)(?:(?:_|-|\s|\.)+(?<absoluteepisode>\d{3}(\.\d{1,2})(?!\d+)))+(?:.+?)\[(?<subgroup>.+?)\].*?(?<hash>\[\w{8}\])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Absolute Episode Number (Year) [SubGroup]
		new(@"^(?<title>.+?)[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2}(?!\d+))[-_. ](\(\d{4}\))[-_. ]\[(?<subgroup>.+?)\]",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Absolute Episode Number [Hash]
		new(
			@"^(?<title>.+?)(?:(?:_|-|\s|\.)+(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:[-_. ]+(?<special>special|ova|ovd))?[-_. ]+.*?(?<hash>\[\w{8}\])(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with airdate AND season/episode number, capture season/epsiode only
		new(
			@"^(?<title>.+?)?\W*(?<airdate>\d{4}\W+[0-1][0-9]\W+[0-3][0-9])(?!\W+[0-3][0-9])[-_. ](?:s?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+)))(?:[ex](?<episode>(?<!\d+)(?:\d{1,3})(?!\d+)))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with airdate AND season/episode number
		new(
			@"^(?<title>.+?)?\W*(?<airyear>\d{4})\W+(?<airmonth>[0-1][0-9])\W+(?<airday>[0-3][0-9])(?!\W+[0-3][0-9]).+?(?:s?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+)))(?:[ex](?<episode>(?<!\d+)(?:\d{1,3})(?!\d+)))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Multi-episode with title (S01E05-06, S01E05-6)
		new(
			@"^(?<title>.+?)(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))E(?<episode>\d{1,2}(?!\d+))(?:-(?<episode>\d{1,2}(?!\d+)))+(?:[-_. ]|$)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with a title, Single episodes (S01E05, 1x05, etc) & Multi-episode (S01E05E06, S01E05-06, S01E05 E06, etc)
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]){1,2}(?<episode>\d{2,3}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)\W?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with a title, 4 digit season number, Single episodes (S2016E05, etc) & Multi-episode (S2016E05E06, S2016E05-06, S2016E05 E06, etc)
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{4})(?!\d+))(?:e|\We|_){1,2}(?<episode>\d{2,4}(?!\d+))(?:(?:\-|e|\We|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)\W?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with a title, 4 digit season number, Single episodes (2016x05, etc) & Multi-episode (2016x05x06, 2016x05-06, 2016x05 x06, etc)
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<season>(?<!\d+)(?:\d{4})(?!\d+))(?:x|\Wx){1,2}(?<episode>\d{2,4}(?!\d+))(?:(?:\-|x|\Wx|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)\W?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Multi-season pack
		new(
			@"^(?<title>.+?)[-_. ]+(?:S|Season[_. ]|Saison[_. ]|Series[_. ])(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:-|[-_. ]{3})(?:S|Season[_. ]|Saison[_. ]|Series[_. ])?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Partial season pack
		new(
			@"^(?<title>.+?)(?:\W+S(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))\W+(?:(?:Part\W?|(?<!\d+\W+)e)(?<seasonpart>\d{1,2}(?!\d+)))+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Mini-Series with year in title, treated as season 1, episodes are labelled as Part01, Part 01, Part.1
		new(@"^(?<title>.+?\d{4})(?:\W+(?:(?:Part\W?|e)(?<episode>\d{1,2}(?!\d+)))+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Mini-Series, treated as season 1, multi episodes are labelled as E1-E2
		new(@"^(?<title>.+?)(?:[-._ ][e])(?<episode>\d{2,3}(?!\d+))(?:(?:\-?[e])(?<episode>\d{2,3}(?!\d+)))+",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with airdate and part (2018.04.28.Part.2)
		new(
			@"^(?<title>.+?)?\W*(?<airyear>\d{4})[-_. ]+(?<airmonth>[0-1][0-9])[-_. ]+(?<airday>[0-3][0-9])(?![-_. ]+[0-3][0-9])[-_. ]+Part[-_. ]?(?<part>[1-9])",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Mini-Series, treated as season 1, episodes are labelled as Part01, Part 01, Part.1
		new(@"^(?<title>.+?)(?:\W+(?:(?:Part\W?|(?<!\d+\W+)e)(?<episode>\d{1,2}(?!\d+)))+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Mini-Series, treated as season 1, episodes are labelled as Part One/Two/Three/...Nine, Part.One, Part_One
		new(@"^(?<title>.+?)(?:\W+(?:Part[-._ ](?<episode>One|Two|Three|Four|Five|Six|Seven|Eight|Nine)(?>[-._ ])))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Mini-Series, treated as season 1, episodes are labelled as XofY
		new(@"^(?<title>.+?)(?:\W+(?:(?<episode>(?<!\d+)\d{1,2}(?!\d+))of\d+)+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Supports Season 01 Episode 03
		new(
			@"(?:.*(?:\""|^))(?<title>.*?)(?:[-_\W](?<![()\[]))+(?:\W?Season\W?)(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:\W|_)+(?:Episode\W)(?:[-_. ]?(?<episode>(?<!\d+)\d{1,2}(?!\d+)))+",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Multi-episode with episodes in square brackets (Series Title [S01E11E12] or Series Title [S01E11-12])
		new(
			@"(?:.*(?:^))(?<title>.*?)[-._ ]+\[S(?<season>(?<!\d+)\d{2}(?!\d+))(?:[E-]{1,2}(?<episode>(?<!\d+)\d{2}(?!\d+)))+\]",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Multi-episode release with no space between series title and season (S01E11E12)
		new(@"(?:.*(?:^))(?<title>.*?)S(?<season>(?<!\d+)\d{2}(?!\d+))(?:E(?<episode>(?<!\d+)\d{2}(?!\d+)))+",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Multi-episode with single episode numbers (S6.E1-E2, S6.E1E2, S6E1E2, etc)
		new(
			@"^(?<title>.+?)[-_. ]S(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:[-_. ]?[ex]?(?<episode>(?<!\d+)\d{1,2}(?!\d+)))+",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Single episode season or episode S1E1 or S1-E1 or S1.Ep1 or S01.Ep.01
		new(
			@"(?:.*(?:\""|^))(?<title>.*?)(?:\W?|_)S(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:\W|_)?Ep?[ ._]?(?<episode>(?<!\d+)\d{1,2}(?!\d+))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//3 digit season S010E05
		new(
			@"(?:.*(?:\""|^))(?<title>.*?)(?:\W?|_)S(?<season>(?<!\d+)\d{3}(?!\d+))(?:\W|_)?E(?<episode>(?<!\d+)\d{1,2}(?!\d+))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//5 digit episode number with a title
		new(
			@"^(?:(?<title>.+?)(?:_|-|\s|\.)+)(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+)))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>(?<!\d+)\d{5}(?!\d+)))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//5 digit multi-episode with a title
		new(
			@"^(?:(?<title>.+?)(?:_|-|\s|\.)+)(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+)))(?:(?:[-_. ]{1,3}ep){1,2}(?<episode>(?<!\d+)\d{5}(?!\d+)))+",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Separated season and episode numbers S01 - E01
		new(@"^(?<title>.+?)(?:_|-|\s|\.)+S(?<season>\d{2}(?!\d+))(\W-\W)E(?<episode>(?<!\d+)\d{2}(?!\d+))(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Season and episode numbers in square brackets (single and mult-episode)
		// Series Title - [02x01] - Episode 1
		// Series Title - [02x01x02] - Episode 1
		new(
			@"^(?<title>.+?)?(?:[-_\W](?<![()\[!]))+\[(?<season>(?<!\d+)\d{1,2})(?:(?:-|x){1,2}(?<episode>\d{2}))+\].+?(?:\.|$)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Anime - Title with season number - Absolute Episode Number (Title S01 - EP14)
		new(@"^(?<title>.+?S\d{1,2})[-_. ]{3,}(?:EP)?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-]))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Anime - French titles with single episode numbers, with or without leading sub group ([RlsGroup] Title - Episode 1)
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)[-_. ]+?(?:Episode[-_. ]+?)(?<absoluteepisode>\d{1}(\.\d{1,2})?(?!\d+))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Anime - 4 digit absolute episode number
		new(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>.+?)[-_. ]+?(?<absoluteepisode>\d{4}(\.\d{1,2})?(?!\d+))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		// Anime - Absolute episode number in square brackets
		new(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>.+?)[-_. ]+?\[(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+))\]",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Season only releases
		new(
			@"^(?<title>.+?)[-_. ]+?(?:S|Season|Saison|Series)[-_. ]?(?<season>\d{1,2}(?![-_. ]?\d+))(?:[-_. ]|$)+(?<extras>EXTRAS|SUBPACK)?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//4 digit season only releases
		new(
			@"^(?<title>.+?)[-_. ]+?(?:S|Season|Saison|Series)[-_. ]?(?<season>\d{4}(?![-_. ]?\d+))(\W+|_|$)(?<extras>EXTRAS|SUBPACK)?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with a title and season/episode in square brackets
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+\[S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>(?<!\d+)\d{2}(?!\d+|i|p)))+\])\W?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Supports 103/113 naming
		new(
			@"^(?<title>.+?)?(?:(?:[_.-](?<![()\[!]))+(?<season>(?<!\d+)[1-9])(?<episode>[1-9][0-9]|[0][1-9])(?![a-z]|\d+))+(?:[_.]|$)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//4 digit episode number
		//Episodes without a title, Single (S01E05, 1x05) AND Multi (S01E04E05, 1x04x05, etc)
		new(
			@"^(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{4}(?!\d+|i|p)))+)(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//4 digit episode number
		//Episodes with a title, Single episodes (S01E05, 1x05, etc) & Multi-episode (S01E05E06, S01E05-06, S01E05 E06, etc)
		new(
			@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{4}(?!\d+|i|p)))+)\W?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with airdate (2018.04.28)
		new(
			@"^(?<title>.+?)?\W*(?<airyear>\d{4})[-_. ]+(?<airmonth>[0-1][0-9])[-_. ]+(?<airday>[0-3][0-9])(?![-_. ]+[0-3][0-9])",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with airdate (04.28.2018)
		new(@"^(?<title>.+?)?\W*(?<airmonth>[0-1][0-9])[-_. ]+(?<airday>[0-3][0-9])[-_. ]+(?<airyear>\d{4})(?!\d+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with airdate (20180428)
		new(@"^(?<title>.+?)?\W*(?<!\d+)(?<airyear>\d{4})(?<airmonth>[0-1][0-9])(?<airday>[0-3][0-9])(?!\d+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Supports 1103/1113 naming
		new(
			@"^(?<title>.+?)?(?:(?:[-_.](?<![()\[!]))*(?<season>(?<!\d+|\(|\[|e|x)\d{2})(?<episode>(?<!e|x)(?:[1-9][0-9]|[0][1-9])(?!p|i|\d+|\)|\]|\W\d+|\W(?:e|ep|x)\d+)))+([-_.]+|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Episodes with single digit episode number (S01E1, S01E5E6, etc)
		new(
			@"^(?<title>.*?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]){1,2}(?<episode>\d{1}))+)+(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//iTunes Season 1\05 Title (Quality).ext
		new(@"^(?:Season(?:_|-|\s|\.)(?<season>(?<!\d+)\d{1,2}(?!\d+)))(?:_|-|\s|\.)(?<episode>(?<!\d+)\d{1,2}(?!\d+))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//iTunes 1-05 Title (Quality).ext
		new(@"^(?:(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:-(?<episode>\d{2,3}(?!\d+))))",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime Range - Title Absolute Episode Number (ep01-12)
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:_|\s|\.)+(?:e|ep)(?<absoluteepisode>\d{2,3}(\.\d{1,2})?)-(?<absoluteepisode>(?<!\d+)\d{1,2}(\.\d{1,2})?(?!\d+|-)).*?(?<hash>\[\w{8}\])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Absolute Episode Number (e66)
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:(?:_|-|\s|\.)+(?:e|ep)(?<absoluteepisode>\d{2,4}(\.\d{1,2})?))+[-_. ].*?(?<hash>\[\w{8}\])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Episode Absolute Episode Number (Series Title Episode 01)
		new(
			@"^(?<title>.+?)[-_. ](?:Episode)(?:[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:_|-|\s|\.)*?(?<hash>\[.{8}\])?(?:$|\.)?",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime Range - Title Absolute Episode Number (1 or 2 digit absolute episode numbers in a range, 1-10)
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)[_. ]+(?<absoluteepisode>(?<!\d+)\d{1,2}(\.\d{1,2})?(?!\d+))-(?<absoluteepisode>(?<!\d+)\d{1,2}(\.\d{1,2})?(?!\d+|-))(?:_|\s|\.)*?(?<hash>\[.{8}\])?(?:$|\.)?",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title Absolute Episode Number
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,4}(\.\d{1,2})?(?!\d+|[ip])))+(?:_|-|\s|\.)*?(?<hash>\[.{8}\])?(?:$|\.)?",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime - Title {Absolute Episode Number}
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+|[ip])))+(?:_|-|\s|\.)*?(?<hash>\[.{8}\])?(?:$|\.)?",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Extant, terrible multi-episode naming (extant.10708.hdtv-lol.mp4)
		new(@"^(?<title>.+?)[-_. ](?<season>[0]?\d?)(?:(?<episode>\d{2}){2}(?!\d+))[-_. ]",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Season only releases for poorly named anime
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ])?(?<title>.+?)[-_. ]+?[\[(](?:S|Season|Saison|Series)[-_. ]?(?<season>\d{1,2}(?![-_. ]?\d+))(?:[-_. )\]]|$)+(?<extras>EXTRAS|SUBPACK)?(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled)
	};

	//Regex to split titles that contain `AKA`.
	private static readonly Regex AlternativeTitleRegex =
		new(@"[ .]+AKA[ .]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Regex to unbracket alternative titles.
	private static readonly Regex BracketedAlternativeTitleRegex =
		new(@"(.*) \([ ]*AKA[ ]+(.*)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex RequestInfoRegex = new(@"^(?:\[.+?\])+", RegexOptions.Compiled);

	private static readonly RegexReplace SimpleTitleRegex = new(
		@"(?:(480|720|1080|2160)[ip]|[xh][\W_]?26[45]|DD\W?5\W1|[<>?*]|848x480|1280x720|1920x1080|3840x2160|4096x2160|(8|10)b(it)?|10-bit)\s*?",
		string.Empty,
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly IParser<IReadOnlyList<Language>> _languageParser;

	private readonly ILogger<ReleaseParserService> _logger;

	private readonly IParser<QualityModel> _qualityParser;

	private readonly IParser<string?> _releaseGroupParser;

	private readonly IParser<StreamingProvider?> _streamingProviderParser;

	public ReleaseParserService(
		ILogger<ReleaseParserService> logger,
		IParser<IReadOnlyList<Language>> languageParser,
		IParser<StreamingProvider?> streamingProviderParser,
		IParser<QualityModel> qualityParser,
		IParser<string?> releaseGroupParser)
	{
		_logger = logger;
		_languageParser = languageParser;
		_streamingProviderParser = streamingProviderParser;
		_qualityParser = qualityParser;
		_releaseGroupParser = releaseGroupParser;
	}

	public BaseRelease Parse(string input)
	{
		_logger.LogDebug("Starting Basic Parse of {Input}", input);

		input = input.Trim();

		input = WebsitePrefixRegex.Replace(input);
		input = WebsitePostfixRegex.Replace(input);

		var releaseTitle = ReleaseUtil.RemoveFileExtension(input);

		releaseTitle = releaseTitle.Replace("【", "[").Replace("】", "]");

		foreach (var replace in PreSubstitutionRegex)
			if (replace.TryReplace(releaseTitle, out releaseTitle))
				_logger.LogDebug("Substituted with {ReleaseTitle}", releaseTitle);

		var simpleTitle = SimpleTitleRegex.Replace(releaseTitle);

		var (main, aliases) = ParseTitle(simpleTitle);

		var languages = _languageParser.Parse(input);
		var quality = _qualityParser.Parse(input);
		var releaseGroup = _releaseGroupParser.Parse(input);
		StreamingProvider? streamingProvider = null;

		if (quality.Resolution.Source is QualitySource.WEB_DL or QualitySource.WEB_RIP)
			streamingProvider = _streamingProviderParser.Parse(input);
		else
			_logger.LogDebug("Skipping Parsing of Streaming Provider for {Input} since Quality is not WebDL or WebRip",
				input);

		var release = new BaseRelease
		{
			FullTitle = input,
			Title = main,
			Aliases = aliases,
			Languages = languages,
			StreamingProvider = streamingProvider,
			Type = ReleaseType.UNKNOWN,
			Quality = quality,
			ReleaseGroup = releaseGroup
		};

		return release;
	}

	private Title ParseTitle(string input)
	{
		//Delete parentheses of the form (aka ...).
		var unbracketedName = BracketedAlternativeTitleRegex.Replace(input, "$1 AKA $2");

		//Split by AKA.
		var titles = AlternativeTitleRegex
			.Split(unbracketedName)
			.Where(alternativeName => alternativeName.IsNotNullOrWhitespace())
			.ToList();

		// Use last part of the splitted Title to go on and take others as aliases.
		var parsableTitle = titles.Last();
		titles.RemoveAt(titles.Count - 1);

		foreach (var regex in ReportTitleRegex)
		{
			var match = regex.Matches(parsableTitle);

			if (match.Count == 0) continue;

			var title = match[0].Groups["title"].Value.Replace('.', ' ').Replace('_', ' ');

			title = RequestInfoRegex.Replace(title, "").Trim(' ');

			if (title.IsNotNullOrWhitespace())
				return new Title(title, titles.Select(alias => alias.Replace(".", " ").Trim()).ToList());
		}

		throw new NotParsableReleaseException("does not match any regex");
	}
}
