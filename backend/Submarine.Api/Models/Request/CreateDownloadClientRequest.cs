using Submarine.Core.Download;

namespace Submarine.Api.Models.Request;

public record CreateDownloadClientRequest
{
	public string Name { get; set; }

	public DownloadClientType Type { get; set; }

	public bool Enable { get; set; }

	public int Priority { get; set; } = 1;

	public string SettingsJson { get; set; }

	public List<string> Tags { get; set; } = new();
}
