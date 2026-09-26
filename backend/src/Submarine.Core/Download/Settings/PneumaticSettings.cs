namespace Submarine.Core.Download;

/// <summary>
///     Folder settings for Pneumatic: nzbs are dropped into a watched folder and a companion .strm
///     placeholder file is written for the Kodi Pneumatic addon to pick up
/// </summary>
public record PneumaticSettings : DownloadClientSettings
{
	/// <summary>Folder new .nzb files are dropped into; must be reachable by Kodi's Pneumatic addon</summary>
	public string NzbFolder { get; init; } = string.Empty;

	/// <summary>Folder .strm placeholder files are written to and read back from</summary>
	public string StrmFolder { get; init; } = string.Empty;
}
