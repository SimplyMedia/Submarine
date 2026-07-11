using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.MediaFile.Naming;

/// <summary>
///     Renders file names from a token based template
/// </summary>
public class NamingTemplateRenderer
{
	private static readonly Regex TokenRegex = new(
		@"(?<prefix>[-_. \[(]*)\{(?<token>[^{}:]+)(?::(?<format>[^{}]+))?\}(?<suffix>[)\]]*)",
		RegexOptions.Compiled);

	private static readonly Regex CleanTitleRemoveRegex = new(@"[^\w\s-]", RegexOptions.Compiled);

	private static readonly Regex CollapseWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

	private static readonly Regex PlaceholderTitleRegex = new(@"^(?:Episode|Ep\.?) \d+$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly Regex InvalidPathCharsRegex = new(@"[<>:""/\\|?*]", RegexOptions.Compiled);

	/// <summary>
	///     Renders a template for a Series episode
	/// </summary>
	/// <param name="template">The naming template</param>
	/// <param name="context">The Series naming context</param>
	/// <returns>The rendered name</returns>
	public RenderedName Render(string template, SeriesNamingContext context)
	{
		var (episodeTitle, usedPlaceholder) = BuildEpisodeTitle(context);

		(string Value, bool EmptySafe) Resolve(string key, string? format)
			=> key switch
			{
				"series title" => (context.SeriesTitle, false),
				"series cleantitle" => (CleanTitle(context.SeriesTitle), false),
				"year" => (context.Year?.ToString() ?? "", false),
				"season" => (Pad(context.SeasonNumber, format), false),
				"episode" => (RenderNumbers(context.EpisodeNumbers, format, "E"), false),
				"absolute" => (RenderNumbers(context.AbsoluteEpisodeNumbers, format, ""), false),
				"episode title" => (episodeTitle, false),
				"quality full" => (QualityFull(context.QualityModel), true),
				"quality title" => (QualityTitle(context.QualityModel), true),
				"release group" => (context.ReleaseGroup ?? "", true),
				"languages" => (RenderLanguages(context.Languages), true),
				_ => ("", false)
			};

		return RenderCore(template, Resolve, usedPlaceholder);
	}

	/// <summary>
	///     Renders a template for a Movie
	/// </summary>
	/// <param name="template">The naming template</param>
	/// <param name="context">The Movie naming context</param>
	/// <returns>The rendered name</returns>
	public RenderedName Render(string template, MovieNamingContext context)
	{
		(string Value, bool EmptySafe) Resolve(string key, string? format)
			=> key switch
			{
				"movie title" => (context.MovieTitle, false),
				"movie cleantitle" => (CleanTitle(context.MovieTitle), false),
				"year" => (context.Year?.ToString() ?? "", false),
				"edition" => (context.Edition ?? "", true),
				"quality full" => (QualityFull(context.QualityModel), true),
				"quality title" => (QualityTitle(context.QualityModel), true),
				"release group" => (context.ReleaseGroup ?? "", true),
				"languages" => (RenderLanguages(context.Languages), true),
				_ => ("", false)
			};

		return RenderCore(template, Resolve, false);
	}

	private static RenderedName RenderCore(string template,
		Func<string, string?, (string Value, bool EmptySafe)> resolve, bool usedPlaceholder)
	{
		var rendered = TokenRegex.Replace(template, match =>
		{
			var key = Canonicalize(match.Groups["token"].Value, out var separator);
			var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

			var (value, emptySafe) = resolve(key, format);

			if (separator != ' ')
				value = value.Replace(' ', separator);

			if (string.IsNullOrEmpty(value) && emptySafe)
				return "";

			return match.Groups["prefix"].Value + value + match.Groups["suffix"].Value;
		});

		return new RenderedName(Sanitize(rendered), usedPlaceholder);
	}

	private static string Canonicalize(string token, out char separator)
	{
		separator = token.Contains('.') ? '.' : token.Contains('_') ? '_' : ' ';

		return token.Replace('.', ' ')
			.Replace('_', ' ')
			.Trim()
			.ToLowerInvariant();
	}

	private static (string Title, bool UsedPlaceholder) BuildEpisodeTitle(SeriesNamingContext context)
	{
		var count = context.EpisodeNumbers.Count > 0
			? context.EpisodeNumbers.Count
			: context.AbsoluteEpisodeNumbers.Count > 0
				? context.AbsoluteEpisodeNumbers.Count
				: context.EpisodeTitles.Count;

		var usedPlaceholder = false;
		var titles = new List<string>(count);

		for (var i = 0; i < count; i++)
		{
			var title = i < context.EpisodeTitles.Count ? context.EpisodeTitles[i] : null;

			if (string.IsNullOrWhiteSpace(title))
			{
				var number = i < context.EpisodeNumbers.Count
					? context.EpisodeNumbers[i]
					: context.AbsoluteEpisodeNumbers[i];

				titles.Add($"Episode {number}");
				usedPlaceholder = true;
			}
			else
			{
				if (PlaceholderTitleRegex.IsMatch(title))
					usedPlaceholder = true;

				titles.Add(title);
			}
		}

		return (string.Join(" + ", titles), usedPlaceholder);
	}

	private static string RenderNumbers(IReadOnlyList<int> numbers, string? format, string separator)
	{
		if (numbers.Count == 0)
			return "";

		var sorted = numbers.OrderBy(n => n).ToList();

		if (sorted.Count == 1)
			return Pad(sorted[0], format);

		var contiguous = sorted[^1] - sorted[0] + 1 == sorted.Count
		                 && sorted.Zip(sorted.Skip(1), (a, b) => b - a == 1).All(x => x);

		return contiguous
			? $"{Pad(sorted[0], format)}-{separator}{Pad(sorted[^1], format)}"
			: string.Join(separator, sorted.Select(n => Pad(n, format)));
	}

	private static string Pad(int number, string? format)
		=> number.ToString().PadLeft(format?.Length ?? 0, '0');

	private static string CleanTitle(string title)
	{
		var stripped = CleanTitleRemoveRegex.Replace(title, "");

		return CollapseWhitespaceRegex.Replace(stripped, " ").Trim();
	}

	private static string QualityTitle(QualityModel? quality)
		=> quality?.Resolution.Name ?? "";

	private static string QualityFull(QualityModel? quality)
	{
		if (quality is null)
			return "";

		var revision = quality.Revision;

		var suffix = revision.IsProper ? " Proper"
			: revision.IsRepack ? " Repack"
			: revision.IsReal ? " REAL"
			: "";

		return quality.Resolution.Name + suffix;
	}

	private static string RenderLanguages(IReadOnlyList<Language> languages)
	{
		if (languages.All(l => l == Language.ENGLISH))
			return "";

		return string.Join("+", languages.Select(TitleCase));
	}

	private static string TitleCase(Language language)
	{
		var name = language.ToString();

		return char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
	}

	private static string Sanitize(string name)
		=> InvalidPathCharsRegex.Replace(name, "")
			.TrimEnd('.', ' ')
			.TrimStart();
}
