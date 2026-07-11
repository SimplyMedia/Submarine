namespace Submarine.Core.DecisionEngine;

/// <summary>
///     The decision made for a candidate Release
/// </summary>
/// <param name="Candidate">The candidate the decision was made for</param>
/// <param name="Approved">Whether the candidate is approved for download</param>
/// <param name="Score">The score of the candidate, higher is preferred</param>
/// <param name="Rejections">The reasons the candidate was rejected, empty if approved</param>
/// <param name="MatchedCustomFormats">The names of the Custom Formats matching the candidate</param>
public record DownloadDecision(
	ReleaseCandidate Candidate,
	bool Approved,
	int Score,
	IReadOnlyList<RejectionReason> Rejections,
	IReadOnlyList<string> MatchedCustomFormats);
