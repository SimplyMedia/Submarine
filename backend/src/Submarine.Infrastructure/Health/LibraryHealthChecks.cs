using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Checks that configured root folders are actually writable, catching read-only mounts that
///     "exist" but reject writes.
/// </summary>
public sealed class MountHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Root folders";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var folders = await db.RootFolders.AsNoTracking().Select(x => x.Path).ToListAsync(cancellationToken);
		var issues = new List<HealthIssueSnapshot>();

		foreach (var path in folders)
		{
			if (!Directory.Exists(path) || IsWritable(path))
			{
				// Missing folders are reported by RootFolderHealthCheck.
				continue;
			}

			issues.Add(new(HealthIssueType.ERROR, Source,
				$"Root folder {path} is mounted read-only", HealthWikiLinks.For("root-folder-mount-ro")));
		}

		return issues;
	}

	private static bool IsWritable(string path)
	{
		var probe = Path.Combine(path, $".submarine-write-test-{Guid.NewGuid():N}");
		try
		{
			using (File.Create(probe))
			{
			}

			File.Delete(probe);
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}
}

/// <summary>
///     Checks that the configured recycle bin folder can be written to.
/// </summary>
public sealed class RecyclingBinHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Recycle bin";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		var path = mediaManagement.RecycleBinPath;
		if (string.IsNullOrEmpty(path))
		{
			return [];
		}

		var probe = Path.Combine(path, $".submarine-write-test-{Guid.NewGuid():N}");
		try
		{
			Directory.CreateDirectory(path);
			using (File.Create(probe))
			{
			}

			File.Delete(probe);
			return [];
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return [new(HealthIssueType.ERROR, Source, $"Unable to write to the recycle bin at {path}", HealthWikiLinks.For("cannot-write-recycle-bin"))];
		}
	}
}

/// <summary>
///     Checks that import lists which add items automatically have a root folder configured.
/// </summary>
public sealed class ImportListRootFolderHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Import lists";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var missing = await db.ImportLists.AsNoTracking()
			.Where(x => x.Enable && x.EnableAutomaticAdd && x.RootFolderId == null)
			.Select(x => x.Name)
			.ToListAsync(cancellationToken);

		return missing.Count == 0
			? []
			: [new(HealthIssueType.ERROR, Source,
				$"Import lists need a root folder to add items automatically: {string.Join(", ", missing)}",
				HealthWikiLinks.For("import-list-missing-root-folder"))];
	}
}

/// <summary>
///     Checks that monitored collections have a root folder configured, so new movies can be added
///     automatically.
/// </summary>
public sealed class MovieCollectionRootFolderHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Collections";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var missing = await db.Collections.AsNoTracking()
			.Where(x => x.Monitored && x.RootFolderId == null)
			.Select(x => x.Title)
			.ToListAsync(cancellationToken);

		return missing.Count == 0
			? []
			: [new(HealthIssueType.ERROR, Source,
				$"Collections need a root folder to add movies automatically: {string.Join(", ", missing)}",
				HealthWikiLinks.For("movie-collection-missing-root-folder"))];
	}
}
