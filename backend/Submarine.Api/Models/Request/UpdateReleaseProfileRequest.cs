namespace Submarine.Api.Models.Request;

public record UpdateReleaseProfileRequest
{
	public string Name { get; set; }

	public bool Enabled { get; set; } = true;

	public List<string> Required { get; set; } = new();

	public List<string> Ignored { get; set; } = new();

	public string? Indexer { get; set; }

	public List<string> Tags { get; set; } = new();
}
