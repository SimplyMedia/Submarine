using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.RootFolders;

/// <summary>
///     Library root folder endpoints.
/// </summary>
public sealed class RootFoldersModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/root-folders");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<RootFolderDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var folders = await db.RootFolders.AsNoTracking().OrderBy(x => x.Path).ToListAsync(cancellationToken);
		var mapped = await db.MediaVersions.AsNoTracking()
			.Select(x => new { x.RootFolderId, x.Path })
			.ToListAsync(cancellationToken);
		return TypedResults.Ok(folders.Select(x => ToDto(
			x,
			mapped.Where(m => m.RootFolderId == x.Id).Select(m => m.Path).ToList())).ToList());
	}

	private static async Task<Ok<RootFolderDto>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var folder = await db.RootFolders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {id} not found");
		var mapped = await db.MediaVersions.AsNoTracking()
			.Where(x => x.RootFolderId == id)
			.Select(x => x.Path)
			.ToListAsync(cancellationToken);
		return TypedResults.Ok(ToDto(folder, mapped));
	}

	private static async Task<Results<Created<RootFolderDto>, ProblemHttpResult>> CreateAsync(
		SubmarineDbContext db,
		IValidator<RootFolderRequest> validator,
		[FromBody] RootFolderRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var path = Path.GetFullPath(request.Path);
		if (!Directory.Exists(path))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Path '{request.Path}' does not exist");
		}

		if (await db.RootFolders.AnyAsync(x => x.Path == path, cancellationToken))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: $"Root folder '{path}' already exists");
		}

		var folder = new RootFolder { Path = path, MediaKind = request.MediaKind };
		db.RootFolders.Add(folder);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Created($"/api/v1/root-folders/{folder.Id}", ToDto(folder, []));
	}

	private static async Task<Results<Ok<RootFolderDto>, ProblemHttpResult>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<RootFolderRequest> validator,
		[FromBody] RootFolderRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var folder = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {id} not found");
		var path = Path.GetFullPath(request.Path);
		if (!Directory.Exists(path))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Path '{request.Path}' does not exist");
		}

		if (await db.RootFolders.AnyAsync(x => x.Path == path && x.Id != id, cancellationToken))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: $"Root folder '{path}' already exists");
		}

		folder.Path = path;
		folder.MediaKind = request.MediaKind;
		await db.SaveChangesAsync(cancellationToken);
		var mapped = await db.MediaVersions.AsNoTracking()
			.Where(x => x.RootFolderId == id)
			.Select(x => x.Path)
			.ToListAsync(cancellationToken);
		return TypedResults.Ok(ToDto(folder, mapped));
	}

	private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var folder = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {id} not found");
		var inUse = await db.MediaVersions.AnyAsync(x => x.RootFolderId == id, cancellationToken)
			|| await db.ImportLists.AnyAsync(x => x.RootFolderId == id, cancellationToken)
			|| await db.Collections.AnyAsync(x => x.RootFolderId == id, cancellationToken);
		if (inUse)
		{
			return TypedResults.Problem(
				statusCode: StatusCodes.Status409Conflict,
				title: $"Root folder '{folder.Path}' is still in use");
		}

		db.RootFolders.Remove(folder);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static RootFolderDto ToDto(RootFolder folder, IReadOnlyList<string> mappedPaths)
	{
		var directory = new DirectoryInfo(folder.Path);
		var accessible = directory.Exists;
		var (free, total) = SpaceOf(folder.Path);
		var mappedSet = mappedPaths
			.Select(p => Path.GetFullPath(Path.Combine(folder.Path, p)))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var unmapped = accessible
			? directory.EnumerateDirectories()
				.Select(x => x.FullName)
				.Where(p => !mappedSet.Contains(p))
				.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
				.ToList()
			: [];
		return new RootFolderDto(folder.Id, folder.Path, folder.MediaKind, accessible, free, total, unmapped);
	}

	private static (long? Free, long? Total) SpaceOf(string path)
	{
		try
		{
			var root = Path.GetPathRoot(Path.GetFullPath(path));
			if (string.IsNullOrEmpty(root))
			{
				return (null, null);
			}

			var drive = new DriveInfo(root);
			return drive.IsReady ? (drive.AvailableFreeSpace, drive.TotalSize) : (null, null);
		}
		catch (ArgumentException)
		{
			return (null, null);
		}
	}
}

/// <summary>Create or update root folder request.</summary>
/// <param name="Path">Absolute path.</param>
/// <param name="MediaKind">Series or movies.</param>
public sealed record RootFolderRequest(string Path, MediaKind MediaKind);

/// <summary>Validator for <see cref="RootFolderRequest" />.</summary>
public sealed class RootFolderRequestValidator : AbstractValidator<RootFolderRequest>
{
	/// <inheritdoc />
	public RootFolderRequestValidator()
	{
		RuleFor(x => x.Path).NotEmpty().Must(x => !x.AsSpan().Contains('\0')).WithMessage("Path must not contain null bytes");
		RuleFor(x => x.MediaKind).IsInEnum();
	}
}

/// <summary>A root folder with space and mapping info.</summary>
/// <param name="Id">Root folder id.</param>
/// <param name="Path">Absolute path.</param>
/// <param name="MediaKind">Series or movies.</param>
/// <param name="Accessible">Whether the path exists.</param>
/// <param name="FreeSpace">Free space on the drive in bytes, null when unknown.</param>
/// <param name="TotalSpace">Total drive size in bytes, null when unknown.</param>
/// <param name="UnmappedFolders">Subfolders not mapped to any version path.</param>
public sealed record RootFolderDto(
	int Id,
	string Path,
	MediaKind MediaKind,
	bool Accessible,
	long? FreeSpace,
	long? TotalSpace,
	List<string> UnmappedFolders);
