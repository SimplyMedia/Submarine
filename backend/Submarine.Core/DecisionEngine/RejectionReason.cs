namespace Submarine.Core.DecisionEngine;

/// <summary>
///     Whether a <see cref="RejectionReason" /> is permanent or may resolve over time
/// </summary>
public enum RejectionType
{
	/// <summary>
	///     The rejection will not resolve on its own
	/// </summary>
	PERMANENT,

	/// <summary>
	///     The rejection may resolve over time
	/// </summary>
	TEMPORARY
}

/// <summary>
///     A reason a candidate Release was rejected
/// </summary>
/// <param name="Reason">The human-readable reason for the rejection</param>
/// <param name="Type">Whether the rejection is permanent or temporary</param>
public record RejectionReason(string Reason, RejectionType Type);
