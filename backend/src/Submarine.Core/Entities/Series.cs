using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A series in the library.
/// </summary>
public sealed class Series : Entity
{
	/// <summary>TheTVDB id, unique.</summary>
	public int TvdbId { get; set; }

	/// <summary>TMDB id.</summary>
	public int? TmdbId { get; set; }

	/// <summary>IMDB id.</summary>
	public string? ImdbId { get; set; }

	/// <summary>Title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Sort friendly title.</summary>
	public string SortTitle { get; set; } = string.Empty;

	/// <summary>Lowercase cleaned title used for parsing and matching.</summary>
	public string CleanTitle { get; set; } = string.Empty;

	/// <summary>Original language code.</summary>
	public string? OriginalLanguage { get; set; }

	/// <summary>Overview.</summary>
	public string? Overview { get; set; }

	/// <summary>Network.</summary>
	public string? Network { get; set; }

	/// <summary>Episode runtime in minutes.</summary>
	public int? Runtime { get; set; }

	/// <summary>First air year.</summary>
	public int? Year { get; set; }

	/// <summary>Poster image URL.</summary>
	public string? PosterUrl { get; set; }

	/// <summary>Backdrop image URL.</summary>
	public string? BackdropUrl { get; set; }

	/// <summary>Airing status.</summary>
	public SeriesStatus Status { get; set; } = SeriesStatus.UPCOMING;

	/// <summary>Series type.</summary>
	public SeriesType Type { get; set; } = SeriesType.STANDARD;

	/// <summary>Metadata provider backing this series.</summary>
	public MetadataProvider MetadataProvider { get; set; } = MetadataProvider.TVDB;

	/// <summary>Episode numbering scheme.</summary>
	public SeriesNumbering Numbering { get; set; } = SeriesNumbering.AIRED;

	/// <summary>Whether the series is monitored.</summary>
	public bool Monitored { get; set; } = true;

	/// <summary>Whether new seasons are monitored automatically.</summary>
	public MonitorNewItems MonitorNewItems { get; set; } = MonitorNewItems.ALL;

	/// <summary>Whether episodes live in season folders.</summary>
	public bool SeasonFolder { get; set; } = true;

	/// <summary>Genres.</summary>
	public List<string> Genres { get; set; } = [];

	/// <summary>Content certification.</summary>
	public string? Certification { get; set; }

	/// <summary>First aired date.</summary>
	public DateTime? FirstAired { get; set; }

	/// <summary>UTC timestamp of the last metadata refresh.</summary>
	public DateTime? LastRefreshedAt { get; set; }

	/// <summary>Tags.</summary>
	public ICollection<Tag> Tags { get; set; } = [];

	/// <summary>Seasons of this series.</summary>
	public ICollection<Season> Seasons { get; set; } = [];

	/// <summary>Episodes of this series.</summary>
	public ICollection<Episode> Episodes { get; set; } = [];

	/// <summary>Versions of this series.</summary>
	public ICollection<MediaVersion> Versions { get; set; } = [];
}
