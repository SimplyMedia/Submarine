using System.ComponentModel.DataAnnotations;
using Submarine.Core.Provider;

namespace Submarine.Api.Models.Request;

/// <summary>
///     A flattened release to grab and send to a download client
/// </summary>
public record GrabReleaseRequest(
	[Required] string Title,
	[Required] string Guid,
	string? DownloadUrl,
	string? Indexer,
	Protocol Protocol,
	long? Size,
	int? SeriesId,
	IReadOnlyList<int>? EpisodeIds,
	int? MovieId);
