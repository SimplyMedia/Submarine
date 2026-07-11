using Submarine.Core.Download;
using Submarine.Core.Languages;
using Submarine.Core.Provider;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A tracked download in the queue, merged with live progress from its download client
/// </summary>
public record QueueItemResponse(
	int Id,
	string DownloadId,
	string Title,
	Protocol Protocol,
	DownloadItemStatus Status,
	string ReleaseTitle,
	string Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup,
	string? Indexer,
	string? OutputPath,
	int? SeriesId,
	int? MovieId,
	IReadOnlyList<int> EpisodeIds,
	long? TotalSize,
	long? RemainingSize,
	TimeSpan? RemainingTime)
{
	public static QueueItemResponse From(TrackedDownload tracked, DownloadClientItem? item)
		=> new(
			tracked.Id,
			tracked.DownloadId,
			tracked.Title,
			tracked.Protocol,
			tracked.Status,
			tracked.ReleaseTitle,
			tracked.Quality.Resolution.Name,
			tracked.Languages,
			tracked.ReleaseGroup,
			tracked.Indexer,
			tracked.OutputPath,
			tracked.SeriesId,
			tracked.MovieId,
			tracked.EpisodeIds,
			item?.TotalSize,
			item?.RemainingSize,
			item?.RemainingTime);
}
