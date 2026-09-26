namespace Submarine.Core.Commands;

// Command records shared across features. Handlers live next to the feature that owns the behaviour.
// A null id means "all items".

public sealed record RefreshSeriesCommand(int? SeriesId = null) : CommandBase;

public sealed record RefreshMovieCommand(int? MovieId = null) : CommandBase;

public sealed record RefreshMetadataCommand : CommandBase;

public sealed record RescanSeriesCommand(int? SeriesId = null) : CommandBase;

public sealed record RescanMovieCommand(int? MovieId = null) : CommandBase;

public sealed record RenameSeriesCommand(int SeriesId, IReadOnlyList<int>? FileIds = null) : CommandBase;

public sealed record RenameMovieCommand(int MovieId, IReadOnlyList<int>? FileIds = null) : CommandBase;

public sealed record MoveSeriesCommand(int SeriesId, int RootFolderId, bool MoveFiles = true) : CommandBase;

public sealed record MoveMovieCommand(int MovieId, int RootFolderId, bool MoveFiles = true) : CommandBase;

public sealed record SeriesSearchCommand(int SeriesId) : CommandBase;

public sealed record SeasonSearchCommand(int SeriesId, int SeasonNumber) : CommandBase;

public sealed record EpisodeSearchCommand(IReadOnlyList<int> EpisodeIds) : CommandBase;

public sealed record MovieSearchCommand(IReadOnlyList<int> MovieIds) : CommandBase;

public sealed record MissingSearchCommand : CommandBase;

public sealed record MissingEpisodeSearchCommand : CommandBase;

public sealed record CutoffUnmetSearchCommand : CommandBase;

public sealed record RssSyncCommand : CommandBase;

public sealed record DownloadMonitorCommand : CommandBase;

public sealed record ProcessPendingReleasesCommand : CommandBase;

public sealed record ImportListSyncCommand(int? ImportListId = null) : CommandBase;

public sealed record BackupCommand : CommandBase;

public sealed record HealthCheckCommand : CommandBase;

public sealed record IndexerDefinitionSyncCommand : CommandBase;

public sealed record RecycleBinCleanupCommand : CommandBase;

public sealed record CheckForUpdateCommand : CommandBase;

public sealed record ClearBlocklistCommand : CommandBase;
