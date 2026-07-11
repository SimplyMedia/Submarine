namespace Submarine.Core.Release;

/// <summary>
///     Movie specific Release Data
/// </summary>
public record MovieReleaseData
{
	/// <summary>
	///     The Edition of this Movie Release, if any
	/// </summary>
	public string? Edition { get; init; }
}
