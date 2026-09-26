namespace Submarine.Core.DecisionEngine;

/// <summary>
///     Decides whether candidate releases should be downloaded and scores them against a <see cref="DecisionContext" />.
/// </summary>
public interface IDownloadDecisionMaker
{
	/// <summary>
	///     Decides whether a candidate release should be downloaded and scores it.
	/// </summary>
	/// <param name="candidate">The candidate release.</param>
	/// <param name="context">The context the decision is made in.</param>
	/// <param name="now">The point in time delay and age windows are evaluated against, defaults to the current UTC time.</param>
	DownloadDecision Decide(ReleaseCandidate candidate, DecisionContext context, DateTime? now = null);

	/// <summary>
	///     Decides and scores all candidates, ordered best first: approved before rejected, then by
	///     <see cref="IReleaseComparer" />.
	/// </summary>
	IReadOnlyList<DownloadDecision> DecideAll(
		IReadOnlyCollection<ReleaseCandidate> candidates,
		DecisionContext context,
		DateTime? now = null);
}
