namespace Submarine.Core.DecisionEngine;

/// <summary>
///     The decision made for a candidate release.
/// </summary>
/// <param name="Candidate">The candidate the decision was made for.</param>
/// <param name="Approved">Whether the candidate is approved for download.</param>
/// <param name="Score">The score of the candidate, higher is preferred, zero when rejected.</param>
/// <param name="Rejections">The reasons the candidate was rejected, empty when approved.</param>
/// <param name="CustomFormats">The custom formats matching the candidate.</param>
/// <param name="CustomFormatScore">The profile score of the matched custom formats.</param>
public sealed record DownloadDecision(
	ReleaseCandidate Candidate,
	bool Approved,
	int Score,
	IReadOnlyList<RejectionReason> Rejections,
	IReadOnlyList<Entities.CustomFormat> CustomFormats,
	int CustomFormatScore);
