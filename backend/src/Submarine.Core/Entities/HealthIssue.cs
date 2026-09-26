using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A current health issue reported by a health check.
/// </summary>
public sealed class HealthIssue : Entity
{
	/// <summary>Severity.</summary>
	public HealthIssueType Type { get; set; }

	/// <summary>Check that reported the issue.</summary>
	public string Source { get; set; } = string.Empty;

	/// <summary>Message.</summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>Link to documentation.</summary>
	public string? WikiUrl { get; set; }
}
