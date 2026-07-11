using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record CreateRootFolderRequest
{
	public string Path { get; set; }

	public MediaKind MediaKind { get; set; }
}
