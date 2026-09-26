using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A configured metadata consumer, writing companion NFO/XML files and images next to imported media.
/// </summary>
public sealed class MetadataConsumer : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Consumer implementation.</summary>
	public MetadataConsumerType Type { get; set; }

	/// <summary>Whether the consumer is enabled.</summary>
	public bool Enable { get; set; } = true;

	/// <summary>Implementation specific settings as JSON.</summary>
	public string SettingsJson { get; set; } = "{}";
}
