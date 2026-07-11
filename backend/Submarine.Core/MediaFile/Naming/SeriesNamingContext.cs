using System.Collections.Generic;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.MediaFile.Naming;

/// <summary>
///     Inputs required to render a file name for a Series episode
/// </summary>
public record SeriesNamingContext
{
	/// <summary>
	///     The Title of the Series
	/// </summary>
	public string SeriesTitle { get; init; } = "";

	/// <summary>
	///     The Season number of the episode
	/// </summary>
	public int SeasonNumber { get; init; }

	/// <summary>
	///     The Episode numbers covered by the file, in order
	/// </summary>
	public IReadOnlyList<int> EpisodeNumbers { get; init; } = new List<int>();

	/// <summary>
	///     The absolute Episode numbers covered by the file, used for Anime
	/// </summary>
	public IReadOnlyList<int> AbsoluteEpisodeNumbers { get; init; } = new List<int>();

	/// <summary>
	///     The Episode titles, aligned to <see cref="EpisodeNumbers" />
	/// </summary>
	public IReadOnlyList<string?> EpisodeTitles { get; init; } = new List<string?>();

	/// <summary>
	///     The Year of the Series, if any
	/// </summary>
	public int? Year { get; init; }

	/// <summary>
	///     The Quality of the file, if any
	/// </summary>
	public QualityModel? QualityModel { get; init; }

	/// <summary>
	///     Languages included in the file, if any
	/// </summary>
	public IReadOnlyList<Language> Languages { get; init; } = new List<Language>();

	/// <summary>
	///     The Release Group of the file, if any
	/// </summary>
	public string? ReleaseGroup { get; init; }

	/// <summary>
	///     If the Series is an Anime
	/// </summary>
	public bool IsAnime { get; init; }
}
