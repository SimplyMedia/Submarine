using Submarine.Core.Download;

namespace Submarine.Core.Entities;

/// <summary>
///     A configured download client instance.
/// </summary>
public sealed class DownloadClient : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Client implementation.</summary>
	public DownloadClientType Type { get; set; }

	/// <summary>Whether the client is enabled.</summary>
	public bool Enable { get; set; } = true;

	/// <summary>Priority, lower is tried first.</summary>
	public int Priority { get; set; } = 1;

	/// <summary>Implementation specific settings as JSON.</summary>
	public string SettingsJson { get; set; } = "{}";

	/// <summary>Remove completed downloads from the client.</summary>
	public bool RemoveCompleted { get; set; }

	/// <summary>Remove failed downloads from the client.</summary>
	public bool RemoveFailed { get; set; }

	/// <summary>Tags.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
