namespace Submarine.Core.Enums;

/// <summary>
///     Severity of a health issue.
/// </summary>
public enum HealthIssueType
{
	/// <summary>Everything is fine.</summary>
	OK,

	/// <summary>Informational notice.</summary>
	NOTICE,

	/// <summary>Warning.</summary>
	WARNING,

	/// <summary>Error.</summary>
	ERROR
}
