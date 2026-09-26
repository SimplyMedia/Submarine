using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;

namespace Submarine.Core.Entities;

/// <summary>
///     A download tracked by the import pipeline.
/// </summary>
public sealed class TrackedDownload : Entity
{
	/// <summary>Id of the download client.</summary>
	public int DownloadClientId { get; set; }

	/// <summary>Download id inside the client.</summary>
	public string DownloadId { get; set; } = string.Empty;

	/// <summary>Client side title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Protocol of the download.</summary>
	public Protocol Protocol { get; set; }

	/// <summary>Client status.</summary>
	public TrackedDownloadStatus Status { get; set; }

	/// <summary>Import pipeline state.</summary>
	public TrackedDownloadState State { get; set; }

	/// <summary>Parsed release title.</summary>
	public string? ReleaseTitle { get; set; }

	/// <summary>Parsed quality.</summary>
	public QualityModel? Quality { get; set; }

	/// <summary>Parsed languages.</summary>
	public List<Language> Languages { get; set; } = [];

	/// <summary>Parsed release group.</summary>
	public string? ReleaseGroup { get; set; }

	/// <summary>Indexer the release was grabbed from.</summary>
	public int? IndexerId { get; set; }

	/// <summary>Output path on the client.</summary>
	public string? OutputPath { get; set; }

	/// <summary>Matched series.</summary>
	public int? SeriesId { get; set; }

	/// <summary>Matched movie.</summary>
	public int? MovieId { get; set; }

	/// <summary>Matched version.</summary>
	public int? MediaVersionId { get; set; }

	/// <summary>Matched episode ids.</summary>
	public List<int> EpisodeIds { get; set; } = [];

	/// <summary>The client this download came from.</summary>
	public DownloadClient DownloadClient { get; set; } = null!;

	/// <summary>Matched series.</summary>
	public Series? Series { get; set; }

	/// <summary>Matched movie.</summary>
	public Movie? Movie { get; set; }

	/// <summary>Matched version.</summary>
	public MediaVersion? MediaVersion { get; set; }

	/// <summary>Indexer the release was grabbed from.</summary>
	public Indexer? Indexer { get; set; }

	/// <summary>Total size in bytes.</summary>
	public long Size { get; set; }

	/// <summary>Remaining size in bytes.</summary>
	public long SizeLeft { get; set; }

	/// <summary>Status messages from the pipeline.</summary>
	public List<string> StatusMessages { get; set; } = [];

	/// <summary>Whether the download was imported.</summary>
	public bool Imported { get; set; }

	/// <summary>UTC timestamp when tracking started.</summary>
	public DateTime Added { get; set; }

	/// <summary>Consecutive poll cycles where the download was missing from the client listing.</summary>
	public int MissedPolls { get; set; }
}
