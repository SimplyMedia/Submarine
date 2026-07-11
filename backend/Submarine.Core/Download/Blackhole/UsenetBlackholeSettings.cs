namespace Submarine.Core.Download.Blackhole;

/// <summary>
///     Folder settings for a <see cref="UsenetBlackholeClient" />
/// </summary>
public record UsenetBlackholeSettings
{
	/// <summary>
	///     Folder new .nzb files are dropped into for an external client to pick up
	/// </summary>
	public required string NzbFolder { get; init; }

	/// <summary>
	///     Folder the external client places completed downloads into
	/// </summary>
	public required string WatchFolder { get; init; }
}
