using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record AddSeriesRequest
{
	public int TvdbId { get; set; }

	public string? Path { get; set; }

	public int? RootFolderId { get; set; }

	public int QualityProfileId { get; set; }

	public int LanguageProfileId { get; set; }

	public SeriesType? Type { get; set; }

	public MetadataProvider? MetadataProvider { get; set; }

	public EpisodeNumbering? Numbering { get; set; }

	public bool Monitored { get; set; } = true;

	/// <summary>
	///     Which Episodes to monitor on add, defaults to all
	/// </summary>
	public MonitorOption Monitor { get; set; } = MonitorOption.ALL;

	/// <summary>
	///     Whether to run an automatic search for the monitored Episodes right after the Series is added
	/// </summary>
	public bool SearchOnAdd { get; set; }

	public bool SeasonFolder { get; set; } = true;

	public List<string> Tags { get; set; } = new();

	/// <summary>
	///     Additional versions to create alongside the default one
	/// </summary>
	public List<AddMediaVersionRequest> Versions { get; set; } = new();
}
