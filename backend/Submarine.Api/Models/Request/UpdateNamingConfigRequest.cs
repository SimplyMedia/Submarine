using Submarine.Core.MediaFile.Naming;

namespace Submarine.Api.Models.Request;

public record UpdateNamingConfigRequest
{
	public bool RenameEpisodes { get; set; }

	public string StandardEpisodeFormat { get; set; } = null!;

	public string AnimeEpisodeFormat { get; set; } = null!;

	public string MovieFormat { get; set; } = null!;

	public string SeriesFolderFormat { get; set; } = null!;

	public string SeasonFolderFormat { get; set; } = null!;

	public string MovieFolderFormat { get; set; } = null!;

	public MultiEpisodeStyle MultiEpisodeStyle { get; set; }
}
