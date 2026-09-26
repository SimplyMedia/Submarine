using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Grab;

/// <summary>
///     The result of attempting to grab a decided release: either sent to a download client, or held as a pending
///     release when every rejection is temporary.
/// </summary>
public abstract record GrabOutcome;

/// <summary>The release was sent to a download client and is now tracked.</summary>
/// <param name="Download">The created tracked download.</param>
public sealed record GrabbedOutcome(TrackedDownload Download) : GrabOutcome;

/// <summary>The release is held back until its delay or availability window clears.</summary>
/// <param name="Pending">The created pending release.</param>
public sealed record PendingOutcome(PendingRelease Pending) : GrabOutcome;

/// <summary>
///     Sends a decided release to a download client and tracks it, or holds it as a pending release when it is only
///     temporarily rejected.
/// </summary>
public interface IGrabService
{
	/// <summary>
	///     Grabs the decision's candidate, or stores it as a pending release when every rejection is temporary.
	/// </summary>
	/// <param name="decision">The decision made for the candidate.</param>
	/// <param name="mediaVersionId">Id of the media version the release is grabbed for.</param>
	/// <param name="seriesId">Id of the matched series, if any.</param>
	/// <param name="episodeIds">Ids of the matched episodes, if any.</param>
	/// <param name="movieId">Id of the matched movie, if any.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">The decision is permanently rejected and cannot be grabbed or held.</exception>
	Task<GrabOutcome> GrabAsync(
		DownloadDecision decision,
		int mediaVersionId,
		int? seriesId,
		IReadOnlyList<int>? episodeIds,
		int? movieId,
		CancellationToken cancellationToken = default);
}
