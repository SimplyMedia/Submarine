using System.Globalization;
using System.Text.RegularExpressions;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.Naming;

/// <summary>
///     Renders naming templates. Tokens are case-insensitive; a dot, underscore or dash inside the token selects the
///     separator-aware spelling ({Series.Title} renders with dots), an all upper or all lower token uppercases or
///     lowercases the value, and a <c>:format</c> suffix pads numbers ({season:00}) or selects a named custom format
///     ({Custom Format:Name}). Tokens that may legitimately be empty drop their adjacent separators when empty.
/// </summary>
internal static partial class NamingTemplateRenderer
{
	private const int MaxNameLength = 255;

	private static readonly Regex TokenRegex = new(
		@"(?<prefix>[-_. \[(]*)\{(?<token>[^{}:]+)(?::(?<format>[^{}]+))?\}(?<suffix>[)\]]*)",
		RegexOptions.Compiled);

	private static readonly Regex CleanTitleRemoveRegex = new(@"[^\w\s-]", RegexOptions.Compiled);

	private static readonly Regex CollapseWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

	private static readonly Regex PlaceholderTitleRegex = new(@"^(?:Episode|Ep\.?) \d+$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex SeasonEpisodeClusterRegex = new(
		@"(?<sprefix>[A-Za-z]*)\{[Ss]eason(?::(?<sformat>[^{}]+))?\}(?<eprefix>[A-Za-z]*)\{[Ee]pisode(?::(?<eformat>[^{}]+))?\}",
		RegexOptions.Compiled);

	private static readonly Regex ColonRegex = new(":", RegexOptions.Compiled);

	private static readonly Regex ColonBeforeSpaceRegex = new(":(?= )", RegexOptions.Compiled);

	/// <summary>
	///     Renders a template with the default sanitation (illegal characters replaced, smart colon replacement).
	/// </summary>
	/// <param name="template">The naming template.</param>
	/// <param name="values">The values the tokens resolve to.</param>
	/// <param name="multiEpisodeStyle">
	///     Style used to render an adjacent Season/Episode token cluster when the file covers multiple episodes.
	///     Range based styles fall back to repeating when the episode numbers are not contiguous.
	/// </param>
	/// <returns>The rendered, filesystem-safe name.</returns>
	public static string Render(
		string template,
		NamingTokenValues values,
		MultiEpisodeStyle multiEpisodeStyle = MultiEpisodeStyle.PREFIXED_RANGE)
		=> Render(template, values, replaceIllegalCharacters: true, ColonReplacement.SMART, multiEpisodeStyle);

	/// <summary>
	///     Renders a template honoring the naming configuration's character handling.
	/// </summary>
	public static string Render(
		string template,
		NamingTokenValues values,
		bool replaceIllegalCharacters,
		ColonReplacement colonReplacement,
		MultiEpisodeStyle multiEpisodeStyle = MultiEpisodeStyle.PREFIXED_RANGE)
	{
		var (episodeTitle, _) = BuildEpisodeTitle(values);

		var template2 = ApplyMultiEpisodeStyle(template, values, multiEpisodeStyle);

		var rendered = TokenRegex.Replace(template2, match =>
		{
			var key = Canonicalize(match.Groups["token"].Value, out var separator, out var casing);
			var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

			var value = ApplyCasing(Resolve(key, format, values, episodeTitle), casing);

			if (separator is '.' or '_' && value.Length > 0)
			{
				value = value.Replace(' ', separator);
			}

			if (value.Length == 0 && IsEmptySafe(key))
			{
				return "";
			}

			return match.Groups["prefix"].Value + value + match.Groups["suffix"].Value;
		});

		return Sanitize(rendered, replaceIllegalCharacters, colonReplacement);
	}

	private static string Resolve(string key, string? format, NamingTokenValues values, string episodeTitle)
		=> key switch
		{
			"series title" => values.SeriesTitle ?? "",
			"series cleantitle" => CleanTitle(values.SeriesTitle ?? ""),
			"series titleyear" => JoinNonEmpty(" ", values.SeriesTitle, values.Year?.ToString()),
			"series titlethe" => TitleThe(values.SeriesTitle),
			"movie title" => values.MovieTitle ?? "",
			"movie cleantitle" => CleanTitle(values.MovieTitle ?? ""),
			"movie titlethe" => TitleThe(values.MovieTitle),
			"year" => values.Year?.ToString() ?? "",
			"season" => Pad(values.SeasonNumber ?? 0, format),
			"episode" => RenderNumbers(values.EpisodeNumbers, format, "E"),
			"absolute" => RenderNumbers(values.AbsoluteEpisodeNumbers, format, ""),
			"episode title" => episodeTitle,
			"episode cleantitle" => CleanTitle(episodeTitle),
			"air date" => values.AirDate ?? "",
			"quality full" => QualityFull(values.Quality),
			"quality title" => values.Quality?.Resolution.Name ?? "",
			"quality proper" => values.Quality is { Revision.IsProper: true } ? "Proper"
				: values.Quality is { Revision.IsRepack: true } ? "Repack"
				: "",
			"quality real" => values.Quality is { Revision.IsReal: true } ? "REAL" : "",
			"release group" => values.ReleaseGroup ?? "",
			"languages" => RenderLanguages(values.Languages),
			"mediainfo simple" => JoinNonEmpty(" ", values.MediaInfo?.VideoCodec, values.MediaInfo?.AudioCodec),
			"mediainfo full" => JoinNonEmpty(" ",
				values.MediaInfo?.VideoCodec,
				values.MediaInfo?.AudioCodec,
				values.MediaInfo?.AudioChannels is null ? null : FormatChannels(values.MediaInfo.AudioChannels.Value),
				values.MediaInfo?.VideoDynamicRange),
			"mediainfo videocodec" => values.MediaInfo?.VideoCodec ?? "",
			"mediainfo audiocodec" => values.MediaInfo?.AudioCodec ?? "",
			"mediainfo audiochannels" => values.MediaInfo?.AudioChannels is null ? "" : FormatChannels(values.MediaInfo.AudioChannels.Value),
			"mediainfo videodynamicrange" => values.MediaInfo?.VideoDynamicRange ?? "",
			"mediainfo videobitdepth" => values.MediaInfo?.VideoBitDepth?.ToString() ?? "",
			"mediainfo audiolanguages" => RenderLanguageNames(values.MediaInfo?.AudioLanguages),
			"mediainfo subtitlelanguages" => RenderLanguageNames(values.MediaInfo?.SubtitleLanguages),
			"custom formats" => string.Join("+", values.CustomFormats),
			"custom format" => values.CustomFormats
				.FirstOrDefault(name => string.Equals(name, format, StringComparison.OrdinalIgnoreCase)) ?? "",
			"original title" => values.OriginalTitle ?? "",
			"original filename" => values.OriginalFilename ?? "",
			"edition tags" => values.Edition ?? "",
			"imdbid" => values.ImdbId ?? "",
			"tvdbid" => values.TvdbId?.ToString() ?? "",
			"tmdbid" => values.TmdbId?.ToString() ?? "",
			_ => ""
		};

	private static string Canonicalize(string token, out char separator, out TokenCasing casing)
	{
		separator = token.Contains('.') ? '.'
			: token.Contains('_') ? '_'
			: ' ';

		casing = TokenCasing.Normal;
		var letters = token.Where(char.IsLetter).ToList();
		if (letters.Count > 0 && letters.All(char.IsUpper))
		{
			casing = TokenCasing.Upper;
		}
		else if (letters.Count > 0 && letters.All(char.IsLower))
		{
			casing = TokenCasing.Lower;
		}

		return token
			.Replace('.', ' ')
			.Replace('_', ' ')
			.Replace('-', ' ')
			.Trim()
			.ToLowerInvariant();
	}

	private static string ApplyCasing(string value, TokenCasing casing) => casing switch
	{
		TokenCasing.Upper => value.ToUpperInvariant(),
		TokenCasing.Lower => value.ToLowerInvariant(),
		_ => value
	};

	// Tokens describing optional metadata render empty without a trace; titles and identifiers always render.
	private static bool IsEmptySafe(string key) => key switch
	{
		"quality full" or "quality title" or "quality proper" or "quality real" or "release group"
			or "languages" or "air date" or "edition tags" or "original title" or "original filename"
			or "custom formats" or "custom format"
			or "mediainfo simple" or "mediainfo full" or "mediainfo videocodec" or "mediainfo audiocodec"
			or "mediainfo audiochannels" or "mediainfo videodynamicrange" or "mediainfo videobitdepth"
			or "mediainfo audiolanguages" or "mediainfo subtitlelanguages" => true,
		_ => false
	};

	/// <summary>
	///     Whether rendering the template would put a placeholder episode title ("Episode 5") into the name,
	///     so the file should be renamed once the real title is known.
	/// </summary>
	public static bool UsesPlaceholderTitle(string template, NamingTokenValues values)
		=> TokenRegex.Matches(template).Any(match => Canonicalize(match.Groups["token"].Value, out _, out _) is "episode title" or "episode cleantitle")
		   && BuildEpisodeTitle(values).UsedPlaceholder;

	private static (string Title, bool UsedPlaceholder) BuildEpisodeTitle(NamingTokenValues values)
	{
		var count = values.EpisodeNumbers.Count > 0
			? values.EpisodeNumbers.Count
			: values.AbsoluteEpisodeNumbers.Count > 0
				? values.AbsoluteEpisodeNumbers.Count
				: values.EpisodeTitles.Count;

		var usedPlaceholder = false;
		var titles = new List<string>(count);

		for (var i = 0; i < count; i++)
		{
			var title = i < values.EpisodeTitles.Count ? values.EpisodeTitles[i] : null;

			if (string.IsNullOrWhiteSpace(title))
			{
				var number = i < values.EpisodeNumbers.Count
					? values.EpisodeNumbers[i]
					: values.AbsoluteEpisodeNumbers[i];

				titles.Add($"Episode {number}");
				usedPlaceholder = true;
			}
			else
			{
				if (PlaceholderTitleRegex.IsMatch(title))
				{
					usedPlaceholder = true;
				}

				titles.Add(title);
			}
		}

		return (string.Join(" + ", titles), usedPlaceholder);
	}

	private static string RenderNumbers(IReadOnlyList<int> numbers, string? format, string separator)
	{
		if (numbers.Count == 0)
		{
			return "";
		}

		var sorted = numbers.OrderBy(n => n).ToList();

		if (sorted.Count == 1)
		{
			return Pad(sorted[0], format);
		}

		var contiguous = sorted[^1] - sorted[0] + 1 == sorted.Count
		                 && sorted.Zip(sorted.Skip(1), (a, b) => b - a == 1).All(x => x);

		return contiguous
			? $"{Pad(sorted[0], format)}-{separator}{Pad(sorted[^1], format)}"
			: string.Join(separator, sorted.Select(n => Pad(n, format)));
	}

	private static string Pad(int number, string? format)
		=> number.ToString().PadLeft(format?.Length ?? 0, '0');

	private static string ApplyMultiEpisodeStyle(string template, NamingTokenValues values, MultiEpisodeStyle style)
	{
		if (values.EpisodeNumbers.Count == 0)
		{
			return template;
		}

		return SeasonEpisodeClusterRegex.Replace(template, match => RenderCluster(
			values.SeasonNumber ?? 0,
			values.EpisodeNumbers,
			match.Groups["sprefix"].Value,
			match.Groups["sformat"].Success ? match.Groups["sformat"].Value : null,
			match.Groups["eprefix"].Value,
			match.Groups["eformat"].Success ? match.Groups["eformat"].Value : null,
			style));
	}

	private static string RenderCluster(int season, IReadOnlyList<int> episodes, string seasonPrefix, string? seasonFormat,
		string episodePrefix, string? episodeFormat, MultiEpisodeStyle style)
	{
		var sorted = episodes.OrderBy(n => n).ToList();
		var seasonPart = seasonPrefix + Pad(season, seasonFormat);

		string Full(int episode) => seasonPart + episodePrefix + Pad(episode, episodeFormat);
		string Repeat() => Full(sorted[0]) + string.Concat(sorted.Skip(1).Select(e => episodePrefix + Pad(e, episodeFormat)));

		if (sorted.Count == 1)
		{
			return Full(sorted[0]);
		}

		var contiguous = sorted[^1] - sorted[0] + 1 == sorted.Count
		                 && sorted.Zip(sorted.Skip(1), (a, b) => b - a == 1).All(x => x);

		return style switch
		{
			MultiEpisodeStyle.DUPLICATE => string.Join(".", sorted.Select(Full)),
			MultiEpisodeStyle.REPEAT => Repeat(),
			MultiEpisodeStyle.SCENE => season.ToString() + string.Concat(sorted.Select(e => "x" + Pad(e, episodeFormat))),
			MultiEpisodeStyle.EXTEND when contiguous => Full(sorted[0])
			                                            + string.Concat(sorted.Skip(1).Select(e => "-" + Pad(e, episodeFormat))),
			MultiEpisodeStyle.RANGE when contiguous => Full(sorted[0]) + "-" + Pad(sorted[^1], episodeFormat),
			MultiEpisodeStyle.PREFIXED_RANGE when contiguous => Full(sorted[0]) + "-" + episodePrefix + Pad(sorted[^1], episodeFormat),
			_ => Repeat()
		};
	}

	private static string FormatChannels(double channels) => channels switch
	{
		2 => "2.0",
		6 => "5.1",
		8 => "7.1",
		_ => channels.ToString("0.0", CultureInfo.InvariantCulture)
	};

	private static string CleanTitle(string title)
	{
		var stripped = CleanTitleRemoveRegex.Replace(title, "");

		return CollapseWhitespaceRegex.Replace(stripped, " ").Trim();
	}

	private static string QualityFull(QualityModel? quality)
	{
		if (quality is null)
		{
			return "";
		}

		var revision = quality.Revision;

		var suffix = revision.IsProper ? " Proper"
			: revision.IsRepack ? " Repack"
			: revision.IsReal ? " REAL"
			: "";

		return quality.Resolution.Name + suffix;
	}

	private static string RenderLanguages(IReadOnlyList<Language> languages)
	{
		if (languages.Count > 0 && languages.All(l => l == Language.ENGLISH))
		{
			return "";
		}

		return string.Join("+", languages.Select(TitleCase));
	}

	private static string RenderLanguageNames(IReadOnlyList<string>? languages)
		=> languages is null || languages.Count == 0 ? "" : string.Join("+", languages);

	private static string TitleCase(Language language)
	{
		var name = language.ToString();

		return char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
	}

	private static string TitleThe(string? title)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return "";
		}

		var parts = title.Split(' ', 2, StringSplitOptions.TrimEntries);

		if (parts.Length != 2 || parts[0].ToLowerInvariant() is not ("the" or "a" or "an"))
		{
			return title;
		}

		var article = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];

		return $"{parts[1]}, {article}";
	}

	private static string JoinNonEmpty(string separator, params string?[] parts)
		=> string.Join(separator, parts.Where(part => !string.IsNullOrEmpty(part)));

	private static string Sanitize(string name, bool replaceIllegalCharacters, ColonReplacement colonReplacement)
	{
		var result = ApplyColonReplacement(name, colonReplacement);

		if (replaceIllegalCharacters)
		{
			result = IllegalPathCharsRegex().Replace(result, "");
		}

		result = CollapseWhitespaceRegex.Replace(result, " ").TrimEnd('.', ' ').TrimStart();

		return result.Length > MaxNameLength ? result[..MaxNameLength].TrimEnd('.', ' ') : result;
	}

	private static string ApplyColonReplacement(string name, ColonReplacement mode)
	{
		switch (mode)
		{
			case ColonReplacement.DELETE:
				return ColonRegex.Replace(name, "");
			case ColonReplacement.DASH:
				return ColonRegex.Replace(name, "-");
			case ColonReplacement.SPACE_DASH:
				return CollapseWhitespaceRegex.Replace(ColonRegex.Replace(name, " -"), " ");
			case ColonReplacement.SPACE_DASH_SPACE:
				return CollapseWhitespaceRegex.Replace(ColonRegex.Replace(name, " - "), " ");
			case ColonReplacement.SMART:
			{
				var replaced = ColonBeforeSpaceRegex.Replace(name, " - ");
				return ColonRegex.Replace(replaced, "");
			}
			default:
				return name;
		}
	}

	[GeneratedRegex(@"[<>:""/\\|?*\x00-\x1f]")]
	private static partial Regex IllegalPathCharsRegex();

	private enum TokenCasing
	{
		Normal,
		Upper,
		Lower
	}
}
