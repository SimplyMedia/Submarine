using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Core.Quality;
using Submarine.Core.Parser;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.QualityOverrides;

/// <summary>
///     Merges the built in release group quality source map with the user defined overrides, cached for 60 seconds.
/// </summary>
public sealed class QualityOverrideSource(IServiceScopeFactory scopeFactory, IMemoryCache cache) : IQualityOverrideSource
{
	private const int CacheSeconds = 60;

	private static readonly IReadOnlyDictionary<string, QualitySource> BuiltIn =
		QualityEdgeCasesConstants.EdgeCaseReleaseGroupQualitySourceMapping;

	private static object CacheKey => typeof(QualityOverrideSource);

	/// <summary>
	///     Release group to quality source mapping applied when a release does not state a quality.
	///     Keys are case-insensitive.
	/// </summary>
	public IReadOnlyDictionary<string, QualitySource> Overrides => GetOverrides();

	/// <summary>
	///     Clears the cached view, used after user overrides change.
	/// </summary>
	public void Invalidate() => cache.Remove(CacheKey);

	private ConcurrentDictionary<string, QualitySource> GetOverrides()
		=> cache.Get(CacheKey) as ConcurrentDictionary<string, QualitySource> ?? Load();

	private ConcurrentDictionary<string, QualitySource> Load()
	{
		var overrides = new ConcurrentDictionary<string, QualitySource>(BuiltIn.ToDictionary(
			entry => entry.Key,
			entry => entry.Value,
			StringComparer.OrdinalIgnoreCase));

		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();

		foreach (var group in db.ReleaseGroupQualityOverrides.AsNoTracking().ToList())
		{
			overrides[group.ReleaseGroup] = group.Source;
		}

		cache.Set(CacheKey, overrides, TimeSpan.FromSeconds(CacheSeconds));

		return overrides;
	}
}
