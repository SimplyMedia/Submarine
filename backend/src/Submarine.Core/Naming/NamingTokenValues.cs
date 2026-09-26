using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.Naming;

/// <summary>
///     The values a naming template can reference. Fields left empty render as empty tokens.
/// </summary>
internal sealed record NamingTokenValues
{
	/// <summary>Title of the series.</summary>
	public string? SeriesTitle { get; init; }

	/// <summary>Title of the movie.</summary>
	public string? MovieTitle { get; init; }

	/// <summary>Release year.</summary>
	public int? Year { get; init; }

	/// <summary>Season number.</summary>
	public int? SeasonNumber { get; init; }

	/// <summary>Episode numbers covered by the file.</summary>
	public IReadOnlyList<int> EpisodeNumbers { get; init; } = [];

	/// <summary>Absolute episode numbers covered by the file, anime only.</summary>
	public IReadOnlyList<int> AbsoluteEpisodeNumbers { get; init; } = [];

	/// <summary>Episode titles aligned to the episode numbers.</summary>
	public IReadOnlyList<string?> EpisodeTitles { get; init; } = [];

	/// <summary>Air date of a daily episode, yyyy-MM-dd.</summary>
	public string? AirDate { get; init; }

	/// <summary>Quality of the file.</summary>
	public QualityModel? Quality { get; init; }

	/// <summary>Languages of the file.</summary>
	public IReadOnlyList<Language> Languages { get; init; } = [];

	/// <summary>Release group of the file.</summary>
	public string? ReleaseGroup { get; init; }

	/// <summary>Edition of the file.</summary>
	public string? Edition { get; init; }

	/// <summary>Technical media information.</summary>
	public MediaInfoTokenValues? MediaInfo { get; init; }

	/// <summary>Original scene title of the release.</summary>
	public string? OriginalTitle { get; init; }

	/// <summary>Original file name before renaming.</summary>
	public string? OriginalFilename { get; init; }

	/// <summary>Names of the custom formats matching the release.</summary>
	public IReadOnlyList<string> CustomFormats { get; init; } = [];

	/// <summary>TheTVDB id.</summary>
	public int? TvdbId { get; init; }

	/// <summary>TheTMDB id.</summary>
	public int? TmdbId { get; init; }

	/// <summary>TheIMDB id.</summary>
	public string? ImdbId { get; init; }
}

/// <summary>
///     Media info values referenced by naming tokens.
/// </summary>
internal sealed record MediaInfoTokenValues
{
	/// <summary>Video codec, for example x264.</summary>
	public string? VideoCodec { get; init; }

	/// <summary>Audio codec, for example dts.</summary>
	public string? AudioCodec { get; init; }

	/// <summary>Number of audio channels.</summary>
	public double? AudioChannels { get; init; }

	/// <summary>Video dynamic range, for example HDR10.</summary>
	public string? VideoDynamicRange { get; init; }

	/// <summary>Video bit depth in bits.</summary>
	public int? VideoBitDepth { get; init; }

	/// <summary>Languages of the audio tracks.</summary>
	public IReadOnlyList<string> AudioLanguages { get; init; } = [];

	/// <summary>Languages of the subtitle tracks.</summary>
	public IReadOnlyList<string> SubtitleLanguages { get; init; } = [];
}
