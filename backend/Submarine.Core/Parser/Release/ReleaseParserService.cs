using System;
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

/// <summary>
///     Service which parses a release
/// </summary>
public class ReleaseParserService : IParser<BaseRelease>
{
	private static readonly Regex EditionRegex = new(
		@"\(?\b(?<edition>(((Recut.|Extended.|Ultimate.)?(Director.?s|Collector.?s|Theatrical|Ultimate|Extended|Despecialized|(Special|Rouge|Final|Assembly|Imperial|Diamond|Signature|Hunter|Rekall)(?=(.(Cut|Edition|Version)))|\d{2,3}(th)?.Anniversary)(?:.(Cut|Edition|Version))?(.(Extended|Uncensored|Remastered|Unrated|Uncut|IMAX|Fan.?Edit))?|((Uncensored|Remastered|Unrated|Uncut|IMAX|Fan.?Edit|Restored|((2|3|4)in1))))))\b\)?",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

	private static readonly Regex[] ReportSeriesTitleRegex =
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

		//Anime - Title (Optional Alias) Season/Episode Year
		new(
			@"^(?<title>.+?)[-_. ](?<alias>\(.+?\))?[-_. ]?(S?(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_ ]?[ex])(?<episode>\d{2,3}(?!\d+)))?)[-_. ](?<titleyear>\d{4})[-_. ]",
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

		//Anime - Title 4-digit Absolute Episode Number [SubGroup]
		new(@"^(?<title>.+?)[-_. ]+(?<absoluteepisode>(?<!\d+)\d{4}(?!\d+))[-_. ]\[(?<subgroup>.+?)\]",
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

	private static readonly Regex[] ReportMovieTitleRegex =
	{
		//Anime [Subgroup] and Year
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)\[!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|x|\d+|\]|\W\d+)))+.*?(?<hash>\[\w{8}\])?(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime [Subgroup] no year, versioned title, hash
		new(
			@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+?)((v)(?:\d{1,2})(?:([-_. ])))(\[.*)?(?:[\[(][^])])?.*?(?<hash>\[\w{8}\])(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime [Subgroup] no year, info in double sets of brackets, hash
		new(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+?)(\[.*).*?(?<hash>\[\w{8}\])(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Anime [Subgroup] no year, info in parentheses or brackets, hash
		new(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+)(?:[\[(][^])]).*?(?<hash>\[\w{8}\])(?:$|\.)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Some german or french tracker formats (missing year, ...) (Only applies to german and TrueFrench releases) - see ParserFixture for examples and tests - french removed as it broke all movies w/ french titles
		new(
			@"^(?<title>(?![(\[]).+?)((\W|_))(" + EditionRegex +
			@".{1,3})?(?:(?<!(19|20)\d{2}.*?)(German|TrueFrench))(.+?)(?=((19|20)\d{2}|$))(?<year>(19|20)\d{2}(?!p|i|\d+|\]|\W\d+))?(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Special, Despecialized, etc. Edition Movies, e.g: Mission.Impossible.3.Special.Edition.2011
		new(
			@"^(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)\[!]))*" + EditionRegex +
			@".{1,3}(?<year>(1(8|9)|20)\d{2}(?!p|i|\d+|\]|\W\d+)))+(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//Normal movie format, e.g: Mission.Impossible.3.2011
		new(
			@"^(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)\[!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|(1(8|9)|20)\d{2}|\]|\W(1(8|9)|20)\d{2})))+(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//PassThePopcorn Torrent names: Star.Wars[PassThePopcorn]
		new(@"^(?<title>.+?)?(?:(?:[-_\W](?<![()\[!]))*(?<year>(\[\w *\])))+(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//That did not work? Maybe some tool uses [] for years. Who would do that?
		new(
			@"^(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|\d+|\W\d+)))+(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled),

		//As a last resort for movies that have ( or [ in their title.
		new(@"^(?<title>.+?)?(?:(?:[-_\W](?<![)\[!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|\d+|\]|\W\d+)))+(\W+|_|$)(?!\\)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled)
	};

	private static readonly Regex[] ReportMovieTitleFolderRegex =
	{
		//When year comes first.
		new(@"^(?:(?:[-_\W](?<![)!]))*(?<year>(19|20)\d{2}(?!p|i|\d+|\W\d+)))+(\W+|_|$)(?<title>.+?)?$")
	};

	//Regex to split titles that contain `AKA`.
	private static readonly Regex AlternativeTitleRegex =
		new(@"[ .]+AKA[ .]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Regex to unbracket alternative titles.
	private static readonly Regex BracketedAlternativeTitleRegex =
		new(@"(.*) \([ ]*AKA[ ]+(.*)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex RequestInfoRegex = new(@"^(?:\[.+?\])+", RegexOptions.Compiled);

	private static readonly Regex ContainsSeriesInformationRegex =
		new(@"[-. _](S\d+(E\d+)?)[-. _]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

	/// <summary>
	///     Parse a BaseRelease from a Release Title.
	/// </summary>
	/// <param name="input">The Release Title</param>
	/// <returns>A <see cref="BaseRelease" /></returns>
	/// <exception cref="ArgumentOutOfRangeException">If an Enum is out of range</exception>
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

		var metadata = ParseTitle(simpleTitle);

		var languages = _languageParser.Parse(input);
		var quality = _qualityParser.Parse(input);
		var releaseGroup = metadata.Group ?? _releaseGroupParser.Parse(input);
		StreamingProvider? streamingProvider = null;

		if (quality.Resolution.Source is QualitySource.WEB_DL or QualitySource.WEB_RIP)
			streamingProvider = _streamingProviderParser.Parse(input);
		else
			_logger.LogDebug("Skipping Parsing of Streaming Provider for {Input} since Quality is not WebDL or WebRip",
				input);

		if (quality.Resolution.Source is QualitySource.UNKNOWN && releaseGroup != null &&
		    QualityEdgeCasesConstants.EdgeCaseReleaseGroupQualitySourceMapping.TryGetValue(releaseGroup,
			    out var qualitySource))
		{
			_logger.LogDebug(
				"{Input} quality source is UNKNOWN but release group has edge case source mapping, applying default source instead",
				input);
			quality.Resolution.Source = qualitySource;
		}

		var type = ReleaseType.UNKNOWN;
		SeriesReleaseData? seriesReleaseData = null;
		MovieReleaseData? movieReleaseData = null;

		var (main, aliases, year, group, hash) = metadata;

		switch (metadata)
		{
			case SeriesTitleMetadata seriesTitleMetadata:
			{
				var (_, _, seasons, episodes, absoluteEpisodes, _, _, _) = seriesTitleMetadata;

				type = ReleaseType.SERIES;

				var parsedSeasons = seasons != null ? (IReadOnlyList<int>)seasons : new List<int>();
				var parsedEpisodes = episodes != null ? (IReadOnlyList<int>)episodes : new List<int>();
				var parsedAbsoluteEpisodes =
					absoluteEpisodes != null ? (IReadOnlyList<int>)absoluteEpisodes : new List<int>();

				var seriesReleaseType = parsedSeasons.Count switch
				{
					> 1 => SeriesReleaseType.MULTI_SEASON,
					1 when parsedEpisodes.Count == 0 => SeriesReleaseType.FULL_SEASON,
					1 when parsedEpisodes.Count > 1 => SeriesReleaseType.PARTIAL_SEASON,
					0 or 1 when parsedEpisodes.Count == 1 => SeriesReleaseType.EPISODE,
					0 when parsedAbsoluteEpisodes.Count > 1 => SeriesReleaseType.MULTI_EPISODES,
					0 when parsedAbsoluteEpisodes.Count == 1 => SeriesReleaseType.EPISODE,
					_ => throw new ArgumentOutOfRangeException()
				};

				seriesReleaseData = new SeriesReleaseData
				{
					Seasons = parsedSeasons,
					Episodes = parsedEpisodes,
					AbsoluteEpisodes = parsedAbsoluteEpisodes,
					ReleaseType = seriesReleaseType
				};
				break;
			}
			case MovieTitleMetadata movieTitleMetadata:
			{
				type = ReleaseType.MOVIE;

				break;
			}
		}

		var release = new BaseRelease
		{
			FullTitle = input,
			Title = main,
			Year = string.IsNullOrEmpty(year) ? null : int.Parse(year),
			Aliases = aliases,
			Languages = languages,
			StreamingProvider = streamingProvider,
			Type = type,
			Quality = quality,
			ReleaseGroup = releaseGroup,
			ReleaseHash = hash,
			SeriesReleaseData = seriesReleaseData,
			MovieReleaseData = movieReleaseData
		};

		return release;
	}

	private TitleMetadata ParseTitle(string input)
	{
		//Delete parentheses of the form (aka ...).
		var unbracketedName = BracketedAlternativeTitleRegex.Replace(input, "$1 AKA $2");

		//Split by AKA.
		var titles = AlternativeTitleRegex
			.Split(unbracketedName)
			.Where(alternativeName => alternativeName.IsNotNullOrWhitespace())
			.Select(alias => alias.Replace(".", " "))
			.ToList();

		// Use last part of the splitted Title to go on and take others as aliases.
		var parsableTitle = titles.Last();
		titles.RemoveAt(titles.Count - 1);

		if (!ContainsSeriesInformationRegex.IsMatch(parsableTitle))
			foreach (var regex in ReportMovieTitleRegex.Concat(ReportMovieTitleFolderRegex))
			{
				var match = regex.Matches(parsableTitle);

				if (match.Count == 0) continue;

				foreach (Match matched in match)
				{
					var title = matched.Groups["title"].Value;
					var year = matched.Groups["titleyear"]?.Value;
					string? group = null;
					string? hash = null;

					var subGroup = matched.Groups["subgroup"];

					if (subGroup.Success) group = subGroup.Value;

					var hashGroup = matched.Groups["hash"];

					if (hashGroup.Success)
					{
						var hashValue = hashGroup.Value.Trim('[', ']');

						if (!hashValue.Equals("1280x720")) hash = hashValue;
					}

					if (title.IsNotNullOrWhitespace())
						return new MovieTitleMetadata(title, titles.Select(t => t.NormalizeReleaseTitle()).ToList(),
							year,
							group, hash, null);
				}
			}

		foreach (var regex in ReportSeriesTitleRegex)
		{
			var match = regex.Matches(parsableTitle);

			if (match.Count == 0) continue;

			foreach (Match matched in match)
			{
				var title = matched.Groups["title"].Value;
				var year = matched.Groups["titleyear"]?.Value;
				var episodes = new List<int>();
				var seasons = new List<int>();
				var absoluteEpisodes = new List<int>();
				var specialAbsoluteEpisodes = new List<decimal>();
				string? group = null;
				string? hash = null;
				var special = false;

				var seasonCaptures = matched.Groups["season"].Captures.ToList();
				var episodeCaptures = matched.Groups["episode"].Captures.ToList();
				var absoluteEpisodeCaptures = matched.Groups["absoluteepisode"].Captures.ToList();

				if (seasonCaptures.Any()) seasons.AddRange(seasonCaptures.Select(s => s.Value.ToInteger()));

				//Allows use to return a list of 0 episodes (We can handle that as a full season release)
				if (episodeCaptures.Any())
				{
					var first = episodeCaptures.First().Value.ToInteger();
					var last = episodeCaptures.Last().Value.ToInteger();

					if (first > last)
						throw new InvalidReleaseException(
							"First episode is greater than last one (invalid release or multiple seasons maybe?)");

					var count = last - first + 1;
					episodes.AddRange(Enumerable.Range(first, count));
				}

				if (absoluteEpisodeCaptures.Any())
				{
					var first = absoluteEpisodeCaptures.First().Value.ToDecimal();
					var last = absoluteEpisodeCaptures.Last().Value.ToDecimal();

					if (first > last)
						throw new InvalidReleaseException(
							"First absolute episode is greater than last one (invalid release or multiple seasons maybe?)");

					if (first % 1 != 0 || last % 1 != 0)
					{
						if (absoluteEpisodeCaptures.Count != 1)
							throw new InvalidReleaseException("Multiple matches not allowed for specials");

						specialAbsoluteEpisodes.Add(first);
						special = true;
					}
					else
					{
						var count = last - first + 1;
						absoluteEpisodes.AddRange(Enumerable.Range((int)first, (int)count).ToArray());

						if (matched.Groups["special"].Success) special = true;
					}
				}

				var subGroup = matched.Groups["subgroup"];

				if (subGroup.Success) group = subGroup.Value;

				var hashGroup = matched.Groups["hash"];

				if (hashGroup.Success)
				{
					var hashValue = hashGroup.Value.Trim('[', ']');

					if (!hashValue.Equals("1280x720")) hash = hashValue;
				}

				if (title.IsNotNullOrWhitespace())
					return new SeriesTitleMetadata(title, titles.Select(t => t.NormalizeReleaseTitle()).ToList(),
						seasons, episodes, absoluteEpisodes, year, group, hash);
			}
		}

		throw new NotParsableReleaseException("does not match any regex");
	}
}
