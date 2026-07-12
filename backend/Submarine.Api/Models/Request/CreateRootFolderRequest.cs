using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record CreateRootFolderRequest
{
	public string Path { get; set; } = null!;

	public MediaKind MediaKind { get; set; }
}
