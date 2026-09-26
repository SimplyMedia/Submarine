namespace Submarine.Core.DecisionEngine;

/// <summary>
///     Whether a rejection may resolve on its own.
/// </summary>
public enum RejectionType
{
	/// <summary>
	///     The rejection will not resolve on its own.
	/// </summary>
	PERMANENT,

	/// <summary>
	///     The rejection may resolve over time, for example once a delay window has passed.
	/// </summary>
	TEMPORARY
}

/// <summary>
///     A reason a candidate release was rejected.
/// </summary>
/// <param name="Reason">The human-readable reason.</param>
/// <param name="Type">Whether the rejection is permanent or temporary.</param>
public sealed record RejectionReason(string Reason, RejectionType Type);
