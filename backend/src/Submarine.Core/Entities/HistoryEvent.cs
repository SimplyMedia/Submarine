using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.Entities;

/// <summary>
///     One library history entry.
/// </summary>
public sealed class HistoryEvent : Entity
{
	/// <summary>Event type.</summary>
	public HistoryEventType Type { get; set; }

	/// <summary>Related series.</summary>
	public int? SeriesId { get; set; }

	/// <summary>Related episode.</summary>
	public int? EpisodeId { get; set; }

	/// <summary>Related movie.</summary>
	public int? MovieId { get; set; }

	/// <summary>Related version.</summary>
	public int? MediaVersionId { get; set; }

	/// <summary>Related series.</summary>
	public Series? Series { get; set; }

	/// <summary>Related episode.</summary>
	public Episode? Episode { get; set; }

	/// <summary>Related movie.</summary>
	public Movie? Movie { get; set; }

	/// <summary>Related version.</summary>
	public MediaVersion? MediaVersion { get; set; }

	/// <summary>Title of the source release.</summary>
	public string SourceTitle { get; set; } = string.Empty;

	/// <summary>Quality of the release or file.</summary>
	public QualityModel? Quality { get; set; }

	/// <summary>Languages of the release or file.</summary>
	public List<Language>? Languages { get; set; }

	/// <summary>Download client id.</summary>
	public string? DownloadId { get; set; }

	/// <summary>Additional event data as JSON.</summary>
	public string? Data { get; set; }

	/// <summary>UTC timestamp of the event.</summary>
	public DateTime Date { get; set; }
}
