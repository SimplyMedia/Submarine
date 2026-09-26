using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A library root folder.
/// </summary>
public sealed class RootFolder : Entity
{
	/// <summary>Absolute path, unique.</summary>
	public string Path { get; set; } = string.Empty;

	/// <summary>Whether this root folder holds series or movies.</summary>
	public MediaKind MediaKind { get; set; }
}
