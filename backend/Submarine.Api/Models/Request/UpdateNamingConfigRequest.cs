using Submarine.Core.MediaFile.Naming;

namespace Submarine.Api.Models.Request;

public record UpdateNamingConfigRequest
{
	public bool RenameEpisodes { get; set; }

	public string StandardEpisodeFormat { get; set; }

	public string AnimeEpisodeFormat { get; set; }

	public string MovieFormat { get; set; }

	public string SeriesFolderFormat { get; set; }

	public string SeasonFolderFormat { get; set; }

	public string MovieFolderFormat { get; set; }

	public MultiEpisodeStyle MultiEpisodeStyle { get; set; }
}
