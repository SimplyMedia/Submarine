using System;
using System.Collections.Generic;
using Submarine.Core.Provider;

namespace Submarine.Core.Indexers;

/// <summary>
///     A raw release as reported by an indexer
/// </summary>
public record ReleaseInfo
{
	/// <summary>
	///     The unique identifier of the release on the indexer
	/// </summary>
	public string Guid { get; init; } = string.Empty;

	/// <summary>
	///     The title of the release
	/// </summary>
	public string Title { get; init; } = string.Empty;

	/// <summary>
	///     The url to download the release from, if any
	/// </summary>
	public string? DownloadUrl { get; init; }

	/// <summary>
	///     The url to the details page of the release, if any
	/// </summary>
	public string? InfoUrl { get; init; }

	/// <summary>
	///     The magnet uri of the release, if any
	/// </summary>
	public string? MagnetUrl { get; init; }

	/// <summary>
	///     The infohash of the torrent, if reported
	/// </summary>
	public string? InfoHash { get; init; }

	/// <summary>
	///     The size of the release in bytes, if reported
	/// </summary>
	public long? Size { get; init; }

	/// <summary>
	///     The date the release was published, if reported
	/// </summary>
	public DateTime? PublishDate { get; init; }

	/// <summary>
	///     The standard Newznab category ids of the release
	/// </summary>
	public IReadOnlyList<int> Categories { get; init; } = [];

	/// <summary>
	///     The amount of seeders, torrent only
	/// </summary>
	public int? Seeders { get; init; }

	/// <summary>
	///     The amount of leechers, torrent only
	/// </summary>
	public int? Leechers { get; init; }

	/// <summary>
	///     The total amount of peers, torrent only
	/// </summary>
	public int? Peers { get; init; }

	/// <summary>
	///     The amount of times the release was downloaded, if reported
	/// </summary>
	public int? Grabs { get; init; }

	/// <summary>
	///     The amount of files in the release, if reported
	/// </summary>
	public int? Files { get; init; }

	/// <summary>
	///     How much of the release counts towards the download volume, 0 = freeleech
	/// </summary>
	public double DownloadVolumeFactor { get; init; } = 1;

	/// <summary>
	///     How much of the release counts towards the upload volume
	/// </summary>
	public double UploadVolumeFactor { get; init; } = 1;

	/// <summary>
	///     The minimum ratio to keep seeding, private trackers only
	/// </summary>
	public double? MinimumRatio { get; init; }

	/// <summary>
	///     The minimum seed time in minutes, private trackers only
	/// </summary>
	public int? MinimumSeedTime { get; init; }

	/// <summary>
	///     The protocol the release is distributed over
	/// </summary>
	public Protocol Protocol { get; init; }

	/// <summary>
	///     The id of the Submarine indexer that returned the release
	/// </summary>
	public int? IndexerId { get; init; }

	/// <summary>
	///     The name of the Submarine indexer that returned the release
	/// </summary>
	public string? Indexer { get; init; }

	/// <summary>
	///     The priority of the Submarine indexer that returned the release
	/// </summary>
	public int IndexerPriority { get; init; } = 25;

	/// <summary>
	///     Indexer specific flags of the release, e.g. freeleech
	/// </summary>
	public IReadOnlyList<IndexerFlag> IndexerFlags { get; init; } = [];

	/// <summary>
	///     The TVDB id of the release, if reported
	/// </summary>
	public int? TvdbId { get; init; }

	/// <summary>
	///     The TMDB id of the release, if reported
	/// </summary>
	public int? TmdbId { get; init; }

	/// <summary>
	///     The IMDb id with tt prefix, if reported
	/// </summary>
	public string? ImdbId { get; init; }

	/// <summary>
	///     The raw language strings of the release, if reported
	/// </summary>
	public IReadOnlyList<string> Languages { get; init; } = [];

	/// <summary>
	///     The poster url of the release, if reported
	/// </summary>
	public string? PosterUrl { get; init; }

	/// <summary>
	///     The description of the release, if reported
	/// </summary>
	public string? Description { get; init; }

	/// <summary>
	///     The age of the release relative to now, null when the publish date is unknown or in the future
	/// </summary>
	public TimeSpan? Age
		=> PublishDate is { } publishDate
			? TimeProvider.System.GetUtcNow() - publishDate is { } age && age > TimeSpan.Zero ? age : null
			: null;
}
