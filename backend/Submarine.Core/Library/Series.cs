using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Library;

/// <summary>
///     A Series is a TV Show which is tracked and monitored for new Episodes
/// </summary>
public class Series : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the series
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     TheTVDB Id of this series, must be unique
	/// </summary>
	public int TvdbId { get; set; }

	/// <summary>
	///     TheMovieDB Id of this series
	/// </summary>
	public int? TmdbId { get; set; }

	/// <summary>
	///     Title of the series
	/// </summary>
	public string Title { get; set; }

	/// <summary>
	///     Title used for sorting this series
	/// </summary>
	public string? SortTitle { get; set; }

	/// <summary>
	///     Overview of the series
	/// </summary>
	public string? Overview { get; set; }

	/// <summary>
	///     Network which airs this series
	/// </summary>
	public string? Network { get; set; }

	/// <summary>
	///     Runtime of an episode in minutes
	/// </summary>
	public int? Runtime { get; set; }

	/// <summary>
	///     Year this series was first aired
	/// </summary>
	public int? Year { get; set; }

	/// <summary>
	///     Status of the series
	/// </summary>
	public SeriesStatus Status { get; set; }

	/// <summary>
	///     Type of the series
	/// </summary>
	public SeriesType Type { get; set; }

	/// <summary>
	///     Path on disk this series is stored at
	/// </summary>
	public string Path { get; set; }

	/// <summary>
	///     Whether this series is monitored for new Episodes
	/// </summary>
	public bool Monitored { get; set; }

	/// <summary>
	///     Whether Episodes of this series are stored in Season subfolders
	/// </summary>
	public bool SeasonFolder { get; set; } = true;

	/// <summary>
	///     Id of the Quality Profile used for this series
	/// </summary>
	public int QualityProfileId { get; set; }

	/// <summary>
	///     Id of the Language Profile used for this series
	/// </summary>
	public int LanguageProfileId { get; set; }

	/// <summary>
	///     Tags of this series
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	///     Seasons of this series
	/// </summary>
	public ICollection<Season> Seasons { get; set; } = new List<Season>();

	/// <summary>
	///     Episodes of this series
	/// </summary>
	public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
}
