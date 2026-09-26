using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;

namespace Submarine.Core.Events;

/// <summary>Shared payload of a grabbed release, used by history, notifications and the queue.</summary>
public sealed record GrabbedRelease(
	string Title,
	string? Indexer,
	int? IndexerId,
	int DownloadClientId,
	string DownloadClientName,
	string DownloadId,
	Protocol Protocol,
	long Size,
	QualityModel Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup,
	int? SeriesId,
	IReadOnlyList<int> EpisodeIds,
	int? MovieId,
	int MediaVersionId,
	string? Guid,
	string? InfoUrl,
	int? CustomFormatScore);

public sealed record SeriesAddedEvent(int SeriesId) : IDomainEvent;

public sealed record SeriesUpdatedEvent(int SeriesId) : IDomainEvent;

public sealed record SeriesDeletedEvent(int SeriesId, string Title, bool DeletedFiles) : IDomainEvent;

public sealed record MovieAddedEvent(int MovieId) : IDomainEvent;

public sealed record MovieUpdatedEvent(int MovieId) : IDomainEvent;

public sealed record MovieDeletedEvent(int MovieId, string Title, bool DeletedFiles) : IDomainEvent;

public sealed record ReleaseGrabbedEvent(GrabbedRelease Release) : IDomainEvent;

public sealed record EpisodeFileImportedEvent(
	int SeriesId,
	int MediaVersionId,
	int EpisodeFileId,
	IReadOnlyList<int> EpisodeIds,
	string Path,
	string SourceTitle,
	string? DownloadId,
	bool IsUpgrade,
	QualityModel Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup) : IDomainEvent;

public sealed record MovieFileImportedEvent(
	int MovieId,
	int MediaVersionId,
	int MovieFileId,
	string Path,
	string SourceTitle,
	string? DownloadId,
	bool IsUpgrade,
	QualityModel Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup) : IDomainEvent;

public enum FileDeleteReason
{
	MANUAL,
	UPGRADE,
	MISSING_FROM_DISK,
	SERIES_DELETED,
	MOVIE_DELETED
}

public sealed record EpisodeFileDeletedEvent(int SeriesId, int MediaVersionId, IReadOnlyList<int> EpisodeIds, string Path, FileDeleteReason Reason) : IDomainEvent;

public sealed record MovieFileDeletedEvent(int MovieId, int MediaVersionId, string Path, FileDeleteReason Reason) : IDomainEvent;

public sealed record RenamedFile(string PreviousPath, string NewPath);

public sealed record MediaRenamedEvent(int? SeriesId, int? MovieId, IReadOnlyList<RenamedFile> Files) : IDomainEvent;

public sealed record DownloadFailedEvent(
	string DownloadId,
	string Title,
	int? SeriesId,
	int? MovieId,
	IReadOnlyList<int> EpisodeIds,
	int? MediaVersionId,
	string Message) : IDomainEvent;

public sealed record ManualInteractionRequiredEvent(string DownloadId, string Title, int? SeriesId, int? MovieId, string Message) : IDomainEvent;

public sealed record QueueUpdatedEvent : IDomainEvent;

public sealed record HealthIssueSnapshot(Enums.HealthIssueType Type, string Source, string Message, string? WikiUrl);

public sealed record HealthIssuesChangedEvent(
	IReadOnlyList<HealthIssueSnapshot> Current,
	IReadOnlyList<HealthIssueSnapshot> Added,
	IReadOnlyList<HealthIssueSnapshot> Restored) : IDomainEvent;

public sealed record IndexerStatusChangedEvent(int IndexerId) : IDomainEvent;

public sealed record DownloadClientStatusChangedEvent(int DownloadClientId) : IDomainEvent;
