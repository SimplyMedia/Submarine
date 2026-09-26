using Microsoft.EntityFrameworkCore;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>Shared native media-management mutation used by native and compatibility APIs.</summary>
public static class MediaManagementConfigService
{
	public static async Task<MediaManagementConfigResource> UpdateAsync(
		SubmarineDbContext db,
		MediaManagementConfigResource request,
		CancellationToken cancellationToken)
	{
		var config = await db.MediaManagementConfig.SingleAsync(cancellationToken);
		config.UseHardlinks = request.UseHardlinks;
		config.ImportExtraFiles = request.ImportExtraFiles;
		config.ExtraFileExtensions = request.ExtraFileExtensions;
		config.MinimumFreeSpaceMb = request.MinimumFreeSpaceMb;
		config.SkipFreeSpaceCheck = request.SkipFreeSpaceCheck;
		config.WriteNfo = request.WriteNfo;
		config.RecycleBinPath = request.RecycleBinPath;
		config.RecycleBinCleanupDays = request.RecycleBinCleanupDays;
		config.CreateEmptySeriesFolders = request.CreateEmptySeriesFolders;
		config.CreateEmptyMovieFolders = request.CreateEmptyMovieFolders;
		config.DeleteEmptyFolders = request.DeleteEmptyFolders;
		config.UnmonitorDeletedFiles = request.UnmonitorDeletedFiles;
		config.ChmodFolder = request.ChmodFolder;
		config.ChmodFile = request.ChmodFile;
		config.ChownGroup = request.ChownGroup;
		config.DownloadPropersAndRepacks = request.DownloadPropersAndRepacks;
		config.EnableMediaInfo = request.EnableMediaInfo;
		await db.SaveChangesAsync(cancellationToken);
		return MediaManagementConfigResource.FromEntity(config);
	}
}
