using System;
using System.Collections.Generic;
using Submarine.Core.Provider;

namespace Submarine.Core.Indexer;

/// <summary>
///     A raw release item as reported by an indexer feed
/// </summary>
public record ReleaseInfo
{
	/// <summary>
	///     The Title of the Release
	/// </summary>
	public string Title { get; init; }

	/// <summary>
	///     The unique identifier of the Release on the indexer
	/// </summary>
	public string Guid { get; init; }

	/// <summary>
	///     The url to download the Release from, if any
	/// </summary>
	public string? DownloadUrl { get; init; }

	/// <summary>
	///     The url to the details page of the Release, if any
	/// </summary>
	public string? InfoUrl { get; init; }

	/// <summary>
	///     The size of the Release in bytes, if reported
	/// </summary>
	public long? Size { get; init; }

	/// <summary>
	///     The date the Release was published, if reported
	/// </summary>
	public DateTimeOffset? PublishDate { get; init; }

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
	///     Indexer specific flags of the Release, e.g. freeleech
	/// </summary>
	public IReadOnlyList<IndexerFlag> IndexerFlags { get; init; } = [];

	/// <summary>
	///     The indexer category ids of the Release
	/// </summary>
	public IReadOnlyList<int> Categories { get; init; } = [];

	/// <summary>
	///     The IMDb id of the Release, if reported
	/// </summary>
	public string? ImdbId { get; init; }

	/// <summary>
	///     The TVDB id of the Release, if reported
	/// </summary>
	public int? TvdbId { get; init; }

	/// <summary>
	///     The TMDB id of the Release, if reported
	/// </summary>
	public int? TmdbId { get; init; }

	/// <summary>
	///     The Protocol the Release is distributed over
	/// </summary>
	public Protocol Protocol { get; init; }
}
