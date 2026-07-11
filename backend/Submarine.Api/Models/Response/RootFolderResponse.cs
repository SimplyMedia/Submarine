using Submarine.Core.Library;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A Root Folder including its currently available free space
/// </summary>
public record RootFolderResponse(int Id, string Path, MediaKind MediaKind, long? FreeSpace)
{
	public static RootFolderResponse FromRootFolder(RootFolder rootFolder)
	{
		long? freeSpace;

		try
		{
			freeSpace = new DriveInfo(rootFolder.Path).AvailableFreeSpace;
		}
		catch
		{
			freeSpace = null;
		}

		return new RootFolderResponse(rootFolder.Id, rootFolder.Path, rootFolder.MediaKind, freeSpace);
	}
}
