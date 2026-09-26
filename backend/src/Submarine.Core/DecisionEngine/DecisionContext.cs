using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     The context a download decision is made in: what is wanted, what already exists and which rules apply.
/// </summary>
public sealed record DecisionContext
{
	/// <summary>The quality profile deciding which qualities are wanted and in which order.</summary>
	public QualityProfile QualityProfile { get; init; } = new();

	/// <summary>The language profile deciding which languages are wanted.</summary>
	public LanguageProfile LanguageProfile { get; init; } = new();

	/// <summary>The quality of the currently held file, if any.</summary>
	public QualityModel? ExistingFileQuality { get; init; }

	/// <summary>The languages of the currently held file, if any.</summary>
	public IReadOnlyList<Language>? ExistingFileLanguages { get; init; }

	/// <summary>Custom format score of the currently held file, used by the upgrade rules.</summary>
	public int ExistingCustomFormatScore { get; init; }

	/// <summary>The release group of the currently held file, used for a repack/version-upgrade group check and a consistency bonus, if any.</summary>
	public string? SeasonReleaseGroup { get; init; }

	/// <summary>The delay profile applicable to the media, if any.</summary>
	public DelayProfile? DelayProfile { get; init; }

	/// <summary>The release profiles applicable to the media, already filtered by tags.</summary>
	public IReadOnlyCollection<ReleaseProfile> ReleaseProfiles { get; init; } = [];

	/// <summary>The release filters applied to candidate releases.</summary>
	public IReadOnlyCollection<ReleaseFilter> ReleaseFilters { get; init; } = [];

	/// <summary>The custom formats scored against candidate releases.</summary>
	public IReadOnlyCollection<CustomFormat> CustomFormats { get; init; } = [];

	/// <summary>The quality definitions carrying per minute size limits.</summary>
	public IReadOnlyCollection<QualityDefinition> QualityDefinitions { get; init; } = [];

	/// <summary>Global indexer behaviour configuration, if loaded.</summary>
	public IndexerConfig? IndexerConfig { get; init; }

	/// <summary>Whether the media's minimum availability is met, null when not applicable.</summary>
	public bool? MinimumAvailabilityMet { get; init; }

	/// <summary>Type of the matched series, if any.</summary>
	public SeriesType? SeriesType { get; init; }

	/// <summary>Runtime of one episode or the movie in minutes, if known.</summary>
	public int? RuntimeMinutes { get; init; }

	/// <summary>How many episodes the release covers, one for movies and single episodes.</summary>
	public int EpisodeCount { get; init; } = 1;

	/// <summary>How propers and repacks are treated.</summary>
	public DownloadPropersAndRepacks DownloadPropersAndRepacks { get; init; } =
		DownloadPropersAndRepacks.PREFER_AND_UPGRADE;

	/// <summary>Returns whether the release is blocklisted, if a blocklist applies.</summary>
	public Func<ReleaseCandidate, bool>? IsBlocklisted { get; init; }

	/// <summary>Releases already queued for the same episodes or movie on this media version.</summary>
	public IReadOnlyCollection<QueuedRelease> QueuedReleases { get; init; } = [];

	/// <summary>Whether an existing episode file covers episodes outside the candidate's set, used to reject partial re-grabs.</summary>
	public bool ExistingFileCoversMoreEpisodes { get; init; }

	/// <summary>Free space available at the media version's path, null when unknown or the check is skipped.</summary>
	public long? AvailableFreeSpaceBytes { get; init; }

	/// <summary>Minimum free space in MB required after the release would be imported.</summary>
	public int MinimumFreeSpaceMb { get; init; } = 100;

	/// <summary>Titles of releases already grabbed and imported with a different quality, held back from being re-grabbed.</summary>
	public IReadOnlySet<string> AlreadyImportedTitles { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>Torrent info hashes of releases already grabbed and imported with a different quality.</summary>
	public IReadOnlySet<string> AlreadyImportedInfoHashes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>Torrent flags required per indexer id before a release from that indexer is grabbed.</summary>
	public IReadOnlyDictionary<int, IReadOnlyList<IndexerFlag>> IndexerRequiredFlags { get; init; } =
		new Dictionary<int, IReadOnlyList<IndexerFlag>>();
}

/// <summary>
///     A release already queued (downloading or importing) for the same episodes or movie, used to avoid re-grabbing
///     while a download is in flight.
/// </summary>
/// <param name="EpisodeIds">Episode ids the queued download covers.</param>
/// <param name="MovieId">Movie id the queued download covers, if any.</param>
/// <param name="Quality">Quality of the queued release.</param>
/// <param name="CustomFormatScore">Custom format score of the queued release.</param>
public sealed record QueuedRelease(IReadOnlyList<int> EpisodeIds, int? MovieId, QualityModel Quality, int CustomFormatScore);
