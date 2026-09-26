using Microsoft.EntityFrameworkCore;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     Constructs configured, enabled download clients from the database.
/// </summary>
public sealed class DownloadClientProvider(SubmarineDbContext db, IDownloadClientFactory factory) : IDownloadClientProvider
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<EnabledDownloadClient>> GetEnabledAsync(Protocol? protocol = null, CancellationToken cancellationToken = default)
	{
		var rows = await db.DownloadClients
			.AsNoTracking()
			.Where(client => client.Enable)
			.OrderBy(client => client.Priority)
			.ToListAsync(cancellationToken);

		var result = new List<EnabledDownloadClient>(rows.Count);
		foreach (var row in rows)
		{
			var instance = CreateSafely(row.Type, row.SettingsJson, row.Id, row.Name);
			if (instance is not null && (protocol is null || instance.Protocol == protocol.Value))
			{
				result.Add(new EnabledDownloadClient(row, instance));
			}
		}

		return result;
	}

	/// <inheritdoc />
	public async Task<EnabledDownloadClient?> GetAsync(int id, CancellationToken cancellationToken = default)
	{
		var row = await db.DownloadClients.AsNoTracking().FirstOrDefaultAsync(client => client.Id == id, cancellationToken);
		if (row is null || !row.Enable)
		{
			return null;
		}

		var instance = CreateSafely(row.Type, row.SettingsJson, row.Id, row.Name);
		return instance is null ? null : new EnabledDownloadClient(row, instance);
	}

	private IDownloadClient? CreateSafely(DownloadClientType type, string settingsJson, int id, string name)
	{
		try
		{
			return factory.Create(type, settingsJson, id, name);
		}
		catch (DownloadClientException)
		{
			return null;
		}
	}
}
