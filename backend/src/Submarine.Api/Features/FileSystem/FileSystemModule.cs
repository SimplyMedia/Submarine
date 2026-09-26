using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Modules;

namespace Submarine.Api.Features.FileSystem;

/// <summary>
///     Read only filesystem browsing for picking paths in the UI.
/// </summary>
public sealed class FileSystemModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
		=> endpoints.MapGet("/api/v1/filesystem", ListAsync);

	private static Results<Ok<FileSystemListingDto>, ProblemHttpResult> ListAsync([FromQuery] string? path, [FromQuery] bool includeFiles = false)
	{
		var fullPath = string.IsNullOrWhiteSpace(path)
			? GetRoot()
			: Path.GetFullPath(path);

		if (!Directory.Exists(fullPath))
		{
			return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Path '{path}' does not exist");
		}

		var directory = new DirectoryInfo(fullPath);
		var directories = directory
			.EnumerateDirectories()
			.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.Select(x => new FileSystemEntryDto(x.Name, x.FullName, null, x.LastWriteTimeUtc))
			.ToList();

		var files = includeFiles
			? directory
				.EnumerateFiles()
				.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
				.Select(x => new FileSystemEntryDto(x.Name, x.FullName, x.Length, x.LastWriteTimeUtc))
				.ToList()
			: [];

		var parent = directory.Parent?.FullName;
		return TypedResults.Ok(new FileSystemListingDto(parent, directories, files));
	}

	private static string GetRoot()
		=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? DriveInfo.GetDrives().First(x => x.IsReady && x.DriveType == DriveType.Fixed).RootDirectory.FullName
			: "/";
}

/// <summary>One directory or file entry.</summary>
/// <param name="Name">Entry name.</param>
/// <param name="Path">Full path.</param>
/// <param name="Size">Size in bytes, files only.</param>
/// <param name="LastModified">UTC last write time.</param>
public sealed record FileSystemEntryDto(string Name, string Path, long? Size, DateTime LastModified);

/// <summary>A directory listing.</summary>
/// <param name="Parent">Parent directory, null at the root.</param>
/// <param name="Directories">Subdirectories.</param>
/// <param name="Files">Files, when requested.</param>
public sealed record FileSystemListingDto(
	string? Parent,
	IReadOnlyList<FileSystemEntryDto> Directories,
	IReadOnlyList<FileSystemEntryDto> Files);
