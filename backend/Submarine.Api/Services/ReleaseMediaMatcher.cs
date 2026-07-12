using System.Text;
using Submarine.Core.Library;
using Submarine.Core.Release;

namespace Submarine.Api.Services;

/// <summary>
///     Matches a parsed release against the library by normalized title (and TVDB id for series)
/// </summary>
public sealed class ReleaseMediaMatcher
{
	private const int YearTolerance = 1;

	private readonly IReadOnlyList<Series> _series;
	private readonly IReadOnlyList<Movie> _movies;

	public ReleaseMediaMatcher(IReadOnlyList<Series> series, IReadOnlyList<Movie> movies)
	{
		_series = series;
		_movies = movies;
	}

	/// <summary>
	///     Normalizes a title to lowercase alphanumerics with collapsed whitespace for comparison
	/// </summary>
	public static string Normalize(string value)
	{
		var builder = new StringBuilder(value.Length);

		foreach (var character in value)
			if (char.IsLetterOrDigit(character))
				builder.Append(char.ToLowerInvariant(character));
			else if (builder.Length > 0 && builder[^1] != ' ')
				builder.Append(' ');

		return builder.ToString().TrimEnd();
	}

	public Series? MatchSeries(BaseRelease release, int? tvdbId)
	{
		if (tvdbId is { } id)
		{
			var byId = _series.FirstOrDefault(s => s.TvdbId == id);

			if (byId != null)
				return byId;
		}

		var titles = TitleForms(release);

		return _series.FirstOrDefault(s => Matches(titles, s.Title, s.SortTitle)
		                                   && YearMatches(release.Year, s.Year));
	}

	public Movie? MatchMovie(BaseRelease release)
	{
		var titles = TitleForms(release);

		return _movies.FirstOrDefault(m => Matches(titles, m.Title, m.SortTitle)
		                                   && YearMatches(release.Year, m.Year));
	}

	private static bool Matches(IReadOnlySet<string> titles, string title, string? sortTitle)
		=> titles.Contains(Normalize(title)) || (sortTitle != null && titles.Contains(Normalize(sortTitle)));

	private static bool YearMatches(int? releaseYear, int? mediaYear)
		=> releaseYear == null || mediaYear == null || Math.Abs(releaseYear.Value - mediaYear.Value) <= YearTolerance;

	private static IReadOnlySet<string> TitleForms(BaseRelease release)
	{
		var forms = new HashSet<string> { Normalize(release.Title) };

		if (release.Aliases != null)
			foreach (var alias in release.Aliases)
				forms.Add(Normalize(alias));

		return forms;
	}
}
