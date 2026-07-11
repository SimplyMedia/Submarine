using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Submarine.Core.Library;

/// <summary>
///     An Episode of a <see cref="Library.Series" />
/// </summary>
public class Episode
{
	/// <summary>
	///     Id of the episode
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Id of the series this episode belongs to
	/// </summary>
	public int SeriesId { get; set; }

	/// <summary>
	///     Season number of this episode
	/// </summary>
	public int SeasonNumber { get; set; }

	/// <summary>
	///     Episode number of this episode within its season
	/// </summary>
	public int EpisodeNumber { get; set; }

	/// <summary>
	///     Absolute episode number of this episode, used for Anime
	/// </summary>
	public int? AbsoluteEpisodeNumber { get; set; }

	/// <summary>
	///     TheTVDB Id of this episode
	/// </summary>
	public int? TvdbId { get; set; }

	/// <summary>
	///     Title of the episode
	/// </summary>
	public string? Title { get; set; }

	/// <summary>
	///     Overview of the episode
	/// </summary>
	public string? Overview { get; set; }

	/// <summary>
	///     Air date of the episode
	/// </summary>
	public DateTimeOffset? AirDate { get; set; }

	/// <summary>
	///     Runtime of the episode in minutes
	/// </summary>
	public int? Runtime { get; set; }

	/// <summary>
	///     Whether this episode is monitored for download
	/// </summary>
	public bool Monitored { get; set; }

	/// <summary>
	///     Id of the Episode File which satisfies this episode, if any
	/// </summary>
	public int? EpisodeFileId { get; set; }
}
