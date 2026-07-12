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

	public bool Monitored { get; set; } = true;

	public bool SeasonFolder { get; set; } = true;

	public List<string> Tags { get; set; } = new();

	/// <summary>
	///     Additional versions to create alongside the default one
	/// </summary>
	public List<AddMediaVersionRequest> Versions { get; set; } = new();
}
