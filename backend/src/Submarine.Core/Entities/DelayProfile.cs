using Submarine.Core.Enums;
using Submarine.Core.Provider;

namespace Submarine.Core.Entities;

/// <summary>
///     Delay applied per protocol before a release is grabbed.
/// </summary>
public sealed class DelayProfile : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Protocol preferred when both are available.</summary>
	public Protocol PreferredProtocol { get; set; } = Protocol.USENET;

	/// <summary>Whether usenet releases are grabbed for tagged media.</summary>
	public bool EnableUsenet { get; set; } = true;

	/// <summary>Whether torrent releases are grabbed for tagged media.</summary>
	public bool EnableTorrent { get; set; } = true;

	/// <summary>Delay in minutes for usenet releases.</summary>
	public int UsenetDelayMinutes { get; set; }

	/// <summary>Delay in minutes for torrent releases.</summary>
	public int TorrentDelayMinutes { get; set; } = 60;

	/// <summary>Bypass the delay when the release is the highest allowed quality.</summary>
	public bool BypassIfHighestQuality { get; set; }

	/// <summary>Bypass the delay when the release scores above the custom format cutoff.</summary>
	public bool BypassIfAboveCustomFormatScore { get; set; }

	/// <summary>Custom format score required for the score bypass.</summary>
	public int MinimumCustomFormatScore { get; set; }

	/// <summary>Sort order, lower matches first.</summary>
	public int Order { get; set; } = 1;

	/// <summary>Tags this profile applies to.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
