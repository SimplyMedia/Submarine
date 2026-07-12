using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Config;

/// <summary>
///     Configuration for how Media Files are managed on disk
/// </summary>
public class MediaManagementConfig : IUpdatable
{
	/// <summary>
	///     Id of the media management config, this is a singleton entity with a fixed Id of 1
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; set; }

	/// <summary>
	///     Whether hardlinks should be used when importing Media Files
	/// </summary>
	public bool UseHardlinks { get; set; } = true;

	/// <summary>
	///     Whether extra files (e.g. subtitles, nfo) should be imported alongside Media Files
	/// </summary>
	public bool ImportExtraFiles { get; set; }

	/// <summary>
	///     Minimum free space in megabytes required on a Root Folder before importing
	/// </summary>
	public int MinimumFreeSpaceMb { get; set; } = 100;

	/// <summary>
	///     Whether an NFO metadata file should be written alongside imported Media Files
	/// </summary>
	public bool WriteNfo { get; set; }

	/// <summary>
	///     Octal file mode (e.g. "755") applied to imported folders. Null disables chmod. Applied on Unix only
	/// </summary>
	public string? ChmodFolder { get; set; }

	/// <summary>
	///     Octal file mode (e.g. "644") applied to imported Media Files. Null disables chmod. Applied on Unix only
	/// </summary>
	public string? ChmodFile { get; set; }

	/// <summary>
	///     User imported folders and Media Files are chowned to. Null disables chown. Applied on Unix only
	/// </summary>
	public string? ChownUser { get; set; }

	/// <summary>
	///     Group imported folders and Media Files are chowned to. Null disables chown. Applied on Unix only
	/// </summary>
	public string? ChownGroup { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
