using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Util;
using Submarine.Core.Util.RegEx;

namespace Submarine.Core.Parser.Release
{
	public class ReleaseParserService : IParser<BaseRelease>
	{
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
		
		private readonly ILogger<ReleaseParserService> _logger;

		private readonly IParser<IReadOnlyList<Language>> _languageParser;

		private readonly IParser<StreamingProvider?> _streamingProviderParser;

		private readonly IParser<QualityModel> _qualityModelParser;

		public ReleaseParserService(
			ILogger<ReleaseParserService> logger,
			IParser<IReadOnlyList<Language>> languageParser,
			IParser<StreamingProvider?> streamingProviderParser,
			IParser<QualityModel> qualityModelParser)
		{
			_logger = logger;
			_languageParser = languageParser;
			_streamingProviderParser = streamingProviderParser;
			_qualityModelParser = qualityModelParser;
		}

		public BaseRelease Parse(string input)
		{
			_logger.LogDebug("Starting Parse of {Input}", input);

			return ParseRelease(input.Trim());
		}

		private BaseRelease ParseRelease(string input)
		{
			var releaseTitle = ReleaseUtil.RemoveFileExtension(input);

			releaseTitle = releaseTitle.Replace("【", "[").Replace("】", "]");

			foreach (var replace in PreSubstitutionRegex)
			{
				if (replace.TryReplace(ref releaseTitle))
					_logger.LogDebug("Substituted with {ReleaseTitle}", releaseTitle);
			}

			var release = new BaseRelease
			{
				FullTitle = input,
				Title = null,
				Aliases = null,

				Languages = _languageParser.Parse(input),
				StreamingProvider = _streamingProviderParser.Parse(input),
				Type = ReleaseType.UNKNOWN,
				SeriesReleaseData = null,
				MovieReleaseData = null,
				Quality = _qualityModelParser.Parse(input),
				Hash = null,
				ReleaseGroup = null,
				CreatedAt = null,
			};

			return release;
		}
	}
}
