namespace Submarine.Core.Entities;

/// <summary>
///     Translation between a download client host path and a local path.
/// </summary>
public sealed class RemotePathMapping : Entity
{
	/// <summary>Host the mapping applies to.</summary>
	public string Host { get; set; } = string.Empty;

	/// <summary>Remote path prefix.</summary>
	public string RemotePath { get; set; } = string.Empty;

	/// <summary>Local path prefix.</summary>
	public string LocalPath { get; set; } = string.Empty;
}
