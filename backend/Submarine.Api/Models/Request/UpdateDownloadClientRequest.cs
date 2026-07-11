namespace Submarine.Api.Models.Request;

public record UpdateDownloadClientRequest
{
	public string? Name { get; set; }

	public bool? Enable { get; set; }

	public int? Priority { get; set; }

	public string? SettingsJson { get; set; }

	public List<string>? Tags { get; set; }
}
