using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>
///     Media management configuration.
/// </summary>
public sealed class MediaManagementConfigModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/config/media-management");
		group.MapGet("/", GetAsync);
		group.MapPut("/", PutAsync);
	}

	private static async Task<Ok<MediaManagementConfigResource>> GetAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(MediaManagementConfigResource.FromEntity(
			await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken)));

	private static async Task<Ok<MediaManagementConfigResource>> PutAsync(
		SubmarineDbContext db,
		IValidator<MediaManagementConfigResource> validator,
		MediaManagementConfigResource request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var config = await db.MediaManagementConfig.SingleAsync(cancellationToken);

		config.UseHardlinks = request.UseHardlinks;
		config.ImportExtraFiles = request.ImportExtraFiles;
		config.ExtraFileExtensions = request.ExtraFileExtensions;
		config.MinimumFreeSpaceMb = request.MinimumFreeSpaceMb;
		config.SkipFreeSpaceCheck = request.SkipFreeSpaceCheck;
		config.FileDate = request.FileDate;
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

		return TypedResults.Ok(MediaManagementConfigResource.FromEntity(config));
	}
}

/// <summary>Media management configuration resource.</summary>
/// <param name="UseHardlinks">Hardlink instead of copy when possible.</param>
/// <param name="ImportExtraFiles">Import extra files like subtitles next to the main file.</param>
/// <param name="ExtraFileExtensions">Comma separated extensions considered extra files.</param>
/// <param name="MinimumFreeSpaceMb">Minimum free space in MB before importing.</param>
/// <param name="SkipFreeSpaceCheck">Skip the free space check.</param>
/// <param name="FileDate">Which date is applied to a media file's modified timestamp on import and rescan.</param>
/// <param name="RecycleBinPath">Recycle bin path, empty to delete immediately.</param>
/// <param name="RecycleBinCleanupDays">Days to keep files in the recycle bin.</param>
/// <param name="CreateEmptySeriesFolders">Create empty series folders on add.</param>
/// <param name="CreateEmptyMovieFolders">Create empty movie folders on add.</param>
/// <param name="DeleteEmptyFolders">Delete empty folders after moves.</param>
/// <param name="UnmonitorDeletedFiles">Unmonitor deleted files found during scans.</param>
/// <param name="ChmodFolder">Octal folder permissions applied on Unix, empty to skip.</param>
/// <param name="ChmodFile">Octal file permissions applied on Unix, empty to skip.</param>
/// <param name="ChownGroup">Group ownership applied on Unix, empty to skip.</param>
/// <param name="DownloadPropersAndRepacks">How propers and repacks are treated during quality decisions.</param>
/// <param name="EnableMediaInfo">Extract media info with ffprobe during import.</param>
public sealed record MediaManagementConfigResource(
	bool UseHardlinks,
	bool ImportExtraFiles,
	string ExtraFileExtensions,
	int MinimumFreeSpaceMb,
	bool SkipFreeSpaceCheck,
	FileDate FileDate,
	string RecycleBinPath,
	int RecycleBinCleanupDays,
	bool CreateEmptySeriesFolders,
	bool CreateEmptyMovieFolders,
	bool DeleteEmptyFolders,
	bool UnmonitorDeletedFiles,
	string ChmodFolder,
	string ChmodFile,
	string ChownGroup,
	DownloadPropersAndRepacks DownloadPropersAndRepacks,
	bool EnableMediaInfo)
{
	/// <summary>Maps the singleton to the resource.</summary>
	public static MediaManagementConfigResource FromEntity(Core.Entities.MediaManagementConfig config)
		=> new(
			config.UseHardlinks,
			config.ImportExtraFiles,
			config.ExtraFileExtensions,
			config.MinimumFreeSpaceMb,
			config.SkipFreeSpaceCheck,
			config.FileDate,
			config.RecycleBinPath,
			config.RecycleBinCleanupDays,
			config.CreateEmptySeriesFolders,
			config.CreateEmptyMovieFolders,
			config.DeleteEmptyFolders,
			config.UnmonitorDeletedFiles,
			config.ChmodFolder,
			config.ChmodFile,
			config.ChownGroup,
			config.DownloadPropersAndRepacks,
			config.EnableMediaInfo);
}

/// <summary>Validator for <see cref="MediaManagementConfigResource" /> PUT requests.</summary>
public sealed class MediaManagementConfigResourceValidator : AbstractValidator<MediaManagementConfigResource>
{
	private const string OctalPattern = "^[0-7]{3,4}$";

	/// <inheritdoc />
	public MediaManagementConfigResourceValidator()
	{
		RuleFor(x => x.MinimumFreeSpaceMb).GreaterThanOrEqualTo(0);
		RuleFor(x => x.RecycleBinCleanupDays).GreaterThanOrEqualTo(0);
		RuleFor(x => x.ChmodFolder).Matches(OctalPattern).When(x => !string.IsNullOrEmpty(x.ChmodFolder));
		RuleFor(x => x.ChmodFile).Matches(OctalPattern).When(x => !string.IsNullOrEmpty(x.ChmodFile));
		RuleFor(x => x.DownloadPropersAndRepacks).IsInEnum();
	}
}
