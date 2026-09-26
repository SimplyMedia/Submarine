using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;
using Submarine.Api.Modules;

namespace Submarine.Api.Features.Compat.Shared;

public sealed class CompatSystemEndpoints : IEndpointModule
{
	public void Map(IEndpointRouteBuilder endpoints)
	{
		MapFacade(endpoints, "sonarr", "v3");
		MapFacade(endpoints, "radarr", "v3");
		MapHealth(endpoints, "prowlarr", "v1");
	}

	private static void MapFacade(IEndpointRouteBuilder endpoints, string facade, string apiVersion)
	{
		var group = CompatRoutes.CreateRestGroup(endpoints, facade, apiVersion);
		group.MapGet("/health", HealthAsync);
		group.MapGet("/diskspace", DiskSpaceAsync);
		group.MapGet("/remotepathmapping", RemotePathMappingsAsync);
		group.MapGet("/filesystem", FileSystemAsync);
	}

	private static void MapHealth(IEndpointRouteBuilder endpoints, string facade, string apiVersion)
		=> CompatRoutes.CreateRestGroup(endpoints, facade, apiVersion).MapGet("/health", HealthAsync);

	private static async Task<IResult> HealthAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var issues = await db.HealthIssues.AsNoTracking()
			.OrderByDescending(x => x.Type)
			.ThenBy(x => x.Source)
			.ThenBy(x => x.Id)
			.Select(x => new CompatHealthIssue(x.Source, TranslateHealthType(x.Type), x.Message, x.WikiUrl))
			.ToListAsync(cancellationToken);
		return Results.Json(issues, CompatJson.Options);
	}

	private static string TranslateHealthType(HealthIssueType type)
		=> type switch
		{
			HealthIssueType.OK => "ok",
			HealthIssueType.NOTICE => "notice",
			HealthIssueType.WARNING => "warning",
			HealthIssueType.ERROR => "error",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown health issue type")
		};

	private static async Task<IResult> DiskSpaceAsync(
		SubmarineDbContext db,
		DataDirectory dataDirectory,
		CancellationToken cancellationToken)
	{
		var roots = await db.RootFolders.AsNoTracking().OrderBy(x => x.Path).ToListAsync(cancellationToken);
		var entries = new List<CompatDiskSpaceEntry>(roots.Count + 1)
		{
			CreateDiskSpaceEntry(dataDirectory.Path, "AppData")
		};
		entries.AddRange(roots.Select(root => CreateDiskSpaceEntry(root.Path, root.Path)));
		return Results.Json(entries, CompatJson.Options);
	}

	private static CompatDiskSpaceEntry CreateDiskSpaceEntry(string path, string label)
	{
		var space = DiskSpace.Query(path);
		return new CompatDiskSpaceEntry(path, label, space?.FreeBytes, space?.TotalBytes);
	}

	private static async Task<IResult> RemotePathMappingsAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var mappings = await db.RemotePathMappings.AsNoTracking()
			.OrderBy(x => x.Host)
			.ThenBy(x => x.RemotePath)
			.ThenBy(x => x.Id)
			.Select(x => new CompatRemotePathMapping(x.Id, x.Host, x.RemotePath, x.LocalPath))
			.ToListAsync(cancellationToken);
		return Results.Json(mappings, CompatJson.Options);
	}

	private static IResult FileSystemAsync(
		string? path,
		bool allowFoldersWithoutTrailingSlashes = false,
		bool includeFiles = false)
	{
		var query = string.IsNullOrWhiteSpace(path) ? GetRoot() : path;

		if (allowFoldersWithoutTrailingSlashes && Directory.Exists(query))
		{
			return Results.Json(LookupContents(query, includeFiles), CompatJson.Options);
		}

		var lastSeparatorIndex = query.LastIndexOf(Path.DirectorySeparatorChar);
		if (lastSeparatorIndex == -1)
		{
			return Results.Json(new CompatFileSystemListing(null, [], []), CompatJson.Options);
		}

		return Results.Json(LookupContents(query[..(lastSeparatorIndex + 1)], includeFiles), CompatJson.Options);
	}

	// Mirrors upstream FileSystemLookupService.GetResult: browsing failures degrade to a
	// partial or empty listing instead of an error response.
	private static CompatFileSystemListing LookupContents(string path, bool includeFiles)
	{
		try
		{
			var parent = GetParent(path);
			var directories = GetDirectories(path);
			var files = includeFiles ? GetFiles(path) : [];
			return new CompatFileSystemListing(parent, directories, files);
		}
		catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
		{
			return new CompatFileSystemListing(GetParent(path), [], []);
		}
		catch (ArgumentException)
		{
			return new CompatFileSystemListing(null, [], []);
		}
	}

	private static List<CompatFileSystemEntry> GetDirectories(string path)
		=> new DirectoryInfo(path).EnumerateDirectories()
			.OrderBy(x => x.Name, StringComparer.Ordinal)
			.Select(x => new CompatFileSystemEntry("folder", x.Name, EnsureTrailingSeparator(x.FullName), null, 0, x.LastWriteTimeUtc))
			.ToList();

	private static List<CompatFileSystemEntry> GetFiles(string path)
		=> new DirectoryInfo(path).EnumerateFiles()
			.OrderBy(x => x.Name, StringComparer.Ordinal)
			.Select(x => new CompatFileSystemEntry("file", x.Name, x.FullName, x.Extension, x.Length, x.LastWriteTimeUtc))
			.ToList();

	private static string? GetParent(string path)
	{
		var parent = new DirectoryInfo(path).Parent;
		return parent is not null ? EnsureTrailingSeparator(parent.FullName) : path == "/" ? null : string.Empty;
	}

	private static string EnsureTrailingSeparator(string path)
		=> Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

	private static string GetRoot()
		=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? DriveInfo.GetDrives().First(x => x.IsReady && x.DriveType == DriveType.Fixed).RootDirectory.FullName
			: "/";
}

public sealed record CompatHealthIssue(string Source, string Type, string Message, string? WikiUrl);

public sealed record CompatDiskSpaceEntry(string Path, string Label, long? FreeSpace, long? TotalSpace);

public sealed record CompatRemotePathMapping(int Id, string Host, string RemotePath, string LocalPath);

public sealed record CompatFileSystemEntry(string Type, string Name, string Path, string? Extension, long Size, DateTime? LastModified);

public sealed record CompatFileSystemListing(
	string? Parent,
	IReadOnlyList<CompatFileSystemEntry> Directories,
	IReadOnlyList<CompatFileSystemEntry> Files);

