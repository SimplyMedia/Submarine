using Microsoft.EntityFrameworkCore;
using Submarine.Core.Download;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.MediaFiles;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Checks that enabled download clients are not placing completed downloads directly inside a
///     configured library root folder, which breaks import because the root folder itself would be
///     picked up as a media folder.
/// </summary>
public sealed class DownloadClientRootFolderHealthCheck(SubmarineDbContext db, IDownloadClientFactory factory) : IHealthCheck
{
	private const string Source = "Download clients";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var rootFolders = await db.RootFolders.AsNoTracking().Select(x => x.Path).ToListAsync(cancellationToken);
		if (rootFolders.Count == 0)
		{
			return [];
		}

		var clients = await db.DownloadClients.AsNoTracking().Where(x => x.Enable)
			.Select(x => new { x.Id, x.Name, x.Type, x.SettingsJson })
			.ToListAsync(cancellationToken);
		if (clients.Count == 0)
		{
			return [];
		}

		var mappings = await db.RemotePathMappings.AsNoTracking().ToListAsync(cancellationToken);
		var issues = new List<HealthIssueSnapshot>();

		foreach (var client in clients)
		{
			IReadOnlyList<string> outputFolders;
			try
			{
				outputFolders = (await factory.Create(client.Type, client.SettingsJson, client.Id, client.Name).GetStatusAsync(cancellationToken)).OutputRootFolders;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// Already reported as unreachable by DownloadClientHealthCheck.
				continue;
			}

			foreach (var folder in outputFolders)
			{
				var local = RemotePathResolver.ToLocal(mappings, client.Name, folder);
				if (rootFolders.Any(root => PathsEqual(root, local)))
				{
					issues.Add(new(HealthIssueType.WARNING, Source,
						$"{client.Name} places downloads directly in the {local} root folder, use a dedicated download folder outside the library",
						HealthWikiLinks.For("downloads-in-root-folder")));
				}
			}
		}

		return issues;
	}

	private static bool PathsEqual(string a, string b)
		=> string.Equals(a.TrimEnd('/', '\\'), b.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
///     Checks that the output folders reported by enabled download clients resolve to a path that
///     exists locally, after applying remote path mappings.
/// </summary>
public sealed class RemotePathMappingHealthCheck(SubmarineDbContext db, IDownloadClientFactory factory) : IHealthCheck
{
	private const string Source = "Download clients";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var downloadConfig = await db.DownloadConfig.AsNoTracking().SingleAsync(cancellationToken);
		if (!downloadConfig.EnableCompletedDownloadHandling)
		{
			// Output folders are not consulted when completed download handling is off.
			return [];
		}

		var clients = await db.DownloadClients.AsNoTracking().Where(x => x.Enable)
			.Select(x => new { x.Id, x.Name, x.Type, x.SettingsJson })
			.ToListAsync(cancellationToken);
		if (clients.Count == 0)
		{
			return [];
		}

		var mappings = await db.RemotePathMappings.AsNoTracking().ToListAsync(cancellationToken);
		var issues = new List<HealthIssueSnapshot>();

		foreach (var client in clients)
		{
			IReadOnlyList<string> outputFolders;
			try
			{
				outputFolders = (await factory.Create(client.Type, client.SettingsJson, client.Id, client.Name).GetStatusAsync(cancellationToken)).OutputRootFolders;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				continue;
			}

			foreach (var folder in outputFolders)
			{
				var local = RemotePathResolver.ToLocal(mappings, client.Name, folder);
				if (!Directory.Exists(local))
				{
					issues.Add(new(HealthIssueType.ERROR, Source,
						$"{client.Name} reports output folder {folder}, which does not exist locally as {local}; add or fix a remote path mapping",
						HealthWikiLinks.For("bad-remote-path-mapping")));
				}
			}
		}

		return issues;
	}
}

/// <summary>
///     Warns when completed download handling is disabled, meaning downloads must be imported manually.
/// </summary>
public sealed class ImportMechanismHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const string Source = "Download handling";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var downloadConfig = await db.DownloadConfig.AsNoTracking().SingleAsync(cancellationToken);
		return downloadConfig.EnableCompletedDownloadHandling
			? []
			: [new(HealthIssueType.WARNING, Source,
				"Completed download handling is disabled, downloads must be imported manually",
				HealthWikiLinks.For("completed-download-handling-is-disabled"))];
	}
}
