using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Languages;
using Submarine.Core.Profile;
using Submarine.Core.Quality;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     The context a download decision is made in, describing what is wanted and what already exists
/// </summary>
public record MediaContext
{
	/// <summary>
	///     The Quality Profile deciding which Qualities are wanted and in which order
	/// </summary>
	public QualityProfile QualityProfile { get; init; }

	/// <summary>
	///     The Language Profile deciding which Languages are wanted
	/// </summary>
	public LanguageProfile LanguageProfile { get; init; }

	/// <summary>
	///     The Filters applied to candidate Releases
	/// </summary>
	public IReadOnlyCollection<ReleaseFilter> Filters { get; init; } = [];

	/// <summary>
	///     The Custom Formats scored against candidate Releases
	/// </summary>
	public IReadOnlyCollection<CustomFormat> CustomFormats { get; init; } = [];

	/// <summary>
	///     The score of each Custom Format by its Id
	/// </summary>
	public IReadOnlyDictionary<int, int> CustomFormatScores { get; init; } = new Dictionary<int, int>();

	/// <summary>
	///     The Quality of the currently held file, if any
	/// </summary>
	public QualityModel? ExistingFileQuality { get; init; }

	/// <summary>
	///     The Languages of the currently held file, if any
	/// </summary>
	public IReadOnlyList<Language>? ExistingFileLanguages { get; init; }

	/// <summary>
	///     The Release Group already imported for the season, used for a consistency bonus, if any
	/// </summary>
	public string? SeasonReleaseGroup { get; init; }

	/// <summary>
	///     The Delay Profile applicable to the media, deciding delay windows and the preferred Protocol, if any
	/// </summary>
	public DelayProfile? DelayProfile { get; init; }

	/// <summary>
	///     The Release Profiles applicable to the media, already filtered to the media's Tags
	/// </summary>
	public IReadOnlyCollection<ReleaseProfile> ReleaseProfiles { get; init; } = [];

	/// <summary>
	///     Whether the media's minimum availability is met, null when the media is not a Movie
	/// </summary>
	public bool? MinimumAvailabilityMet { get; init; }
}
