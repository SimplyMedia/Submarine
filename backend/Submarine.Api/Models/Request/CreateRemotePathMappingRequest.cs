namespace Submarine.Api.Models.Request;

public record CreateRemotePathMappingRequest
{
	public string Host { get; set; }

	public string RemotePath { get; set; }

	public string LocalPath { get; set; }
}
