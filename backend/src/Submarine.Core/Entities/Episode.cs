namespace Submarine.Core.Entities;

/// <summary>
///     An episode of a series.
/// </summary>
public sealed class Episode : Entity
{
	/// <summary>Id of the owning series.</summary>
	public int SeriesId { get; set; }

	/// <summary>Season number, 0 for specials.</summary>
	public int SeasonNumber { get; set; }

	/// <summary>Episode number within the season.</summary>
	public int EpisodeNumber { get; set; }

	/// <summary>Absolute episode number, for anime numbering.</summary>
	public int? AbsoluteEpisodeNumber { get; set; }

	/// <summary>Scene season number.</summary>
	public int? SceneSeasonNumber { get; set; }

	/// <summary>Scene episode number.</summary>
	public int? SceneEpisodeNumber { get; set; }

	/// <summary>Scene absolute episode number.</summary>
	public int? SceneAbsoluteEpisodeNumber { get; set; }

	/// <summary>TheTVDB episode id.</summary>
	public int? TvdbId { get; set; }

	/// <summary>TMDB episode id.</summary>
	public int? TmdbId { get; set; }

	/// <summary>Title.</summary>
	public string? Title { get; set; }

	/// <summary>Overview.</summary>
	public string? Overview { get; set; }

	/// <summary>Air date, yyyy-MM-dd.</summary>
	public string? AirDate { get; set; }

	/// <summary>Air date and time in UTC.</summary>
	public DateTime? AirDateUtc { get; set; }

	/// <summary>Runtime in minutes.</summary>
	public int? Runtime { get; set; }

	/// <summary>Whether the episode is monitored.</summary>
	public bool Monitored { get; set; } = true;

	/// <summary>UTC timestamp of the last automatic search.</summary>
	public DateTime? LastSearchTime { get; set; }

	/// <summary>Owning series.</summary>
	public Series Series { get; set; } = null!;

	/// <summary>Files containing this episode.</summary>
	public ICollection<EpisodeFile> Files { get; set; } = [];
}
