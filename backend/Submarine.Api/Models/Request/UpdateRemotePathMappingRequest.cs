namespace Submarine.Api.Models.Request;

public record UpdateRemotePathMappingRequest
{
	public string Host { get; set; } = null!;

	public string RemotePath { get; set; } = null!;

	public string LocalPath { get; set; } = null!;
}
