namespace Submarine.Api.Models.Request;

public record CreateRemotePathMappingRequest
{
	public string Host { get; set; } = null!;

	public string RemotePath { get; set; } = null!;

	public string LocalPath { get; set; } = null!;
}
