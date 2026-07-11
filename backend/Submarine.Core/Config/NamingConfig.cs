using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.MediaFile.Naming;

namespace Submarine.Core.Config;

/// <summary>
///     Configuration for how imported Media Files should be renamed
/// </summary>
public class NamingConfig : IUpdatable
{
	/// <summary>
	///     Id of the naming config, this is a singleton entity with a fixed Id of 1
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; set; }

	/// <summary>
	///     Whether Episodes should be renamed on import
	/// </summary>
	public bool RenameEpisodes { get; set; } = true;

	/// <summary>
	///     Naming template for standard Series episodes
	/// </summary>
	public string StandardEpisodeFormat { get; set; } = NamingDefaults.SeriesFileName;

	/// <summary>
	///     Naming template for Anime episodes
	/// </summary>
	public string AnimeEpisodeFormat { get; set; } = NamingDefaults.AnimeFileName;

	/// <summary>
	///     Naming template for Movies
	/// </summary>
	public string MovieFormat { get; set; } = NamingDefaults.MovieFileName;

	/// <summary>
	///     Naming template for Series folders
	/// </summary>
	public string SeriesFolderFormat { get; set; } = "{Series Title}";

	/// <summary>
	///     Naming template for Season folders
	/// </summary>
	public string SeasonFolderFormat { get; set; } = "Season {Season:00}";

	/// <summary>
	///     Naming template for Movie folders
	/// </summary>
	public string MovieFolderFormat { get; set; } = "{Movie Title} ({Year})";

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
