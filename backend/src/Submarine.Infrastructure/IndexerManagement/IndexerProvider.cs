using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Default <see cref="IIndexerProvider" />. Scoped so it can read the database; capability caching lives in the
///     singleton <see cref="IndexerCapabilityCache" /> injected alongside it.
/// </summary>
/// <param name="db">The database context.</param>
/// <param name="factory">Builds live indexer clients from an implementation and settings.</param>
/// <param name="definitionLoader">Loads Cardigann definitions for informational metadata.</param>
/// <param name="capabilityCache">The shared capability cache.</param>
/// <param name="timeProvider">The time source, used to evaluate backoff windows.</param>
public sealed class IndexerProvider(
	SubmarineDbContext db,
	IIndexerFactory factory,
	IndexerDefinitionLoader definitionLoader,
	IndexerCapabilityCache capabilityCache,
	TimeProvider timeProvider) : IIndexerProvider
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<ConfiguredIndexer>> GetEnabledAsync(IndexerSearchMode mode, CancellationToken cancellationToken = default)
	{
		var query = db.Indexers.Include(indexer => indexer.Proxy).Include(indexer => indexer.Tags).AsNoTracking().AsQueryable();
		query = mode switch
		{
			IndexerSearchMode.RSS => query.Where(indexer => indexer.EnableRss),
			IndexerSearchMode.AUTOMATIC => query.Where(indexer => indexer.EnableAutomaticSearch),
			IndexerSearchMode.INTERACTIVE => query.Where(indexer => indexer.EnableInteractiveSearch),
			_ => query
		};

		var entities = await query.ToListAsync(cancellationToken);
		if (entities.Count == 0)
		{
			return [];
		}

		var ids = entities.Select(entity => entity.Id).ToList();
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var disabledIds = await db.IndexerStatuses.AsNoTracking()
			.Where(status => ids.Contains(status.IndexerId) && status.DisabledUntil != null && status.DisabledUntil > now)
			.Select(status => status.IndexerId)
			.ToListAsync(cancellationToken);

		var available = entities.Where(entity => !disabledIds.Contains(entity.Id)).ToList();
		var configured = new List<ConfiguredIndexer>(available.Count);

		foreach (var entity in available)
		{
			configured.Add(new ConfiguredIndexer(entity, await CreateAsync(entity, cancellationToken)));
		}

		return configured;
	}

	/// <inheritdoc />
	public async Task<IIndexer> CreateAsync(Indexer entity, CancellationToken cancellationToken = default)
	{
		var proxySettings = await ResolveProxyAsync(entity, cancellationToken);
		var definitionInfo = entity.Implementation == IndexerImplementation.CARDIGANN && entity.DefinitionId is { } definitionId
			? definitionLoader.Load(definitionId)?.ToInfo()
			: null;

		return new StampedIndexer(factory.Create(entity.Implementation, entity.SettingsJson, proxySettings, definitionInfo), entity.Id, entity.Name);
	}

	/// <inheritdoc />
	public async Task<IndexerCapabilities> GetCapabilitiesAsync(Indexer entity, CancellationToken cancellationToken = default)
	{
		var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(entity.SettingsJson)));
		if (capabilityCache.TryGet(entity.Id, hash, out var cached))
		{
			return cached;
		}

		await using var indexer = await CreateAsync(entity, cancellationToken);
		var capabilities = await indexer.GetCapabilitiesAsync(cancellationToken);
		capabilityCache.Set(entity.Id, hash, capabilities);
		return capabilities;
	}

	/// <inheritdoc />
	public void InvalidateCapabilities(int indexerId)
		=> capabilityCache.Invalidate(indexerId);

	private async Task<IndexerProxySettings?> ResolveProxyAsync(Indexer entity, CancellationToken cancellationToken)
	{
		var proxy = entity.Proxy;

		if (proxy is null && entity.ProxyId is { } proxyId)
		{
			proxy = await db.IndexerProxies.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == proxyId, cancellationToken);
		}
		else if (proxy is null && entity.ProxyId is null && entity.Tags.Count > 0)
		{
			var tagIds = entity.Tags.Select(tag => tag.Id).ToList();
			proxy = await db.IndexerProxies.Include(candidate => candidate.Tags).AsNoTracking()
				.FirstOrDefaultAsync(candidate => candidate.Tags.Any(tag => tagIds.Contains(tag.Id)), cancellationToken);
		}

		return proxy is null
			? null
			: new IndexerProxySettings(MapProxyType(proxy.Type), proxy.Host, proxy.Port, proxy.Username, proxy.Password, proxy.RequestTimeoutSeconds);
	}

	private static IndexerProxyType MapProxyType(Core.Enums.IndexerProxyType type)
		=> type switch
		{
			Core.Enums.IndexerProxyType.HTTP => IndexerProxyType.HTTP,
			Core.Enums.IndexerProxyType.SOCKS4 => IndexerProxyType.SOCKS4,
			Core.Enums.IndexerProxyType.SOCKS5 => IndexerProxyType.SOCKS5,
			Core.Enums.IndexerProxyType.FLARESOLVERR => IndexerProxyType.FLARESOLVERR,
			_ => IndexerProxyType.HTTP
		};
}
