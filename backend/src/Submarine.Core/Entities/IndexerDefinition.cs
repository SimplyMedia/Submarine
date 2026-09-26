using Submarine.Core.Enums;
using Submarine.Core.Provider;

namespace Submarine.Core.Entities;

/// <summary>
///     A Cardigann definition file cached from the definition repository.
/// </summary>
public sealed class IndexerDefinition : Entity
{
	/// <summary>Definition id from the repository, unique.</summary>
	public string DefinitionId { get; set; } = string.Empty;

	/// <summary>Indexer name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Description.</summary>
	public string? Description { get; set; }

	/// <summary>Language of the indexer.</summary>
	public string? Language { get; set; }

	/// <summary>Access type.</summary>
	public IndexerDefinitionType Type { get; set; }

	/// <summary>Protocol of the indexer.</summary>
	public Protocol Protocol { get; set; }

	/// <summary>Links, usually tracker and repository URLs.</summary>
	public List<string> Links { get; set; } = [];

	/// <summary>Raw Cardigann YAML.</summary>
	public string Yaml { get; set; } = string.Empty;

	/// <summary>Definition version.</summary>
	public string Version { get; set; } = string.Empty;

	/// <summary>UTC timestamp the upstream definition was last updated.</summary>
	public DateTime? UpstreamUpdatedAt { get; set; }
}
