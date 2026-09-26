using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     Import and file management configuration, singleton row with Id 1.
/// </summary>
public sealed class MediaManagementConfig : SingletonEntity
{
	/// <summary>Hardlink instead of copy when possible.</summary>
	public bool UseHardlinks { get; set; } = true;

	/// <summary>Import extra files like subtitles next to the main file.</summary>
	public bool ImportExtraFiles { get; set; }

	/// <summary>Comma separated extensions considered extra files.</summary>
	public string ExtraFileExtensions { get; set; } = "nfo";

	/// <summary>Minimum free space in MB before importing.</summary>
	public int MinimumFreeSpaceMb { get; set; } = 100;

	/// <summary>Skip the free space check.</summary>
	public bool SkipFreeSpaceCheck { get; set; }

	/// <summary>Which date is applied to a media file's modified timestamp on import and rescan.</summary>
	public FileDate FileDate { get; set; } = FileDate.NONE;

	/// <summary>Recycle bin path, empty to delete immediately.</summary>
	public string RecycleBinPath { get; set; } = string.Empty;

	/// <summary>Days to keep files in the recycle bin.</summary>
	public int RecycleBinCleanupDays { get; set; } = 7;

	/// <summary>Create empty series folders on add.</summary>
	public bool CreateEmptySeriesFolders { get; set; }

	/// <summary>Create empty movie folders on add.</summary>
	public bool CreateEmptyMovieFolders { get; set; }

	/// <summary>Delete empty folders after moves.</summary>
	public bool DeleteEmptyFolders { get; set; }

	/// <summary>Unmonitor deleted files found during scans.</summary>
	public bool UnmonitorDeletedFiles { get; set; }

	/// <summary>Octal folder permissions applied on Unix, empty to skip.</summary>
	public string ChmodFolder { get; set; } = string.Empty;

	/// <summary>Octal file permissions applied on Unix, empty to skip.</summary>
	public string ChmodFile { get; set; } = string.Empty;

	/// <summary>Group ownership applied on Unix, empty to skip.</summary>
	public string ChownGroup { get; set; } = string.Empty;

	/// <summary>How propers and repacks are preferred.</summary>
	public DownloadPropersAndRepacks DownloadPropersAndRepacks { get; set; } = DownloadPropersAndRepacks.PREFER_AND_UPGRADE;

	/// <summary>Extract media info with ffprobe during import.</summary>
	public bool EnableMediaInfo { get; set; } = true;
}
