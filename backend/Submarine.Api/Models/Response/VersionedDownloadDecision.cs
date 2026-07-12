using Submarine.Core.DecisionEngine;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A <see cref="DownloadDecision" /> paired with the version it was decided for, if any
/// </summary>
public record VersionedDownloadDecision(DownloadDecision Decision, int? VersionId, string? VersionName);
