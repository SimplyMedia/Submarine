namespace Submarine.Core.Entities;

/// <summary>
///     Download handling configuration, singleton row with Id 1.
/// </summary>
public sealed class DownloadConfig : SingletonEntity
{
	/// <summary>Import completed downloads automatically.</summary>
	public bool EnableCompletedDownloadHandling { get; set; } = true;

	/// <summary>Remove imported downloads from the client.</summary>
	public bool RemoveCompletedDownloads { get; set; }

	/// <summary>Handle failed downloads.</summary>
	public bool EnableFailedDownloadHandling { get; set; } = true;

	/// <summary>Automatically redownload failed releases.</summary>
	public bool RedownloadFailedReleases { get; set; } = true;

	/// <summary>Remove failed downloads from the client.</summary>
	public bool RemoveFailedDownloads { get; set; } = true;

	/// <summary>Minutes between finished download checks.</summary>
	public int CheckForFinishedDownloadInterval { get; set; } = 1;
}
