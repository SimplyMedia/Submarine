using Submarine.Core.Events;

namespace Submarine.Core.Notifications;

/// <summary>
///     Published when the update checker finds a newer release.
/// </summary>
/// <param name="CurrentVersion">Version of the running instance.</param>
/// <param name="NewVersion">Version of the newest release.</param>
/// <param name="ReleaseNotesUrl">Url of the release notes.</param>
public sealed record ApplicationUpdateAvailableEvent(
	string CurrentVersion,
	string NewVersion,
	string ReleaseNotesUrl) : IDomainEvent;
