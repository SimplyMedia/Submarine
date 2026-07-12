namespace Submarine.Api.Models.Request;

public record UpdateDownloadConfigRequest
{
	public bool EnableFailedDownloadHandling { get; set; }

	public bool RedownloadFailedReleases { get; set; }

	public bool RemoveFailedFromClient { get; set; }
}
