namespace Submarine.Api.Models.Request;

public record CreateTagRequest
{
	public string Label { get; set; } = null!;
}
