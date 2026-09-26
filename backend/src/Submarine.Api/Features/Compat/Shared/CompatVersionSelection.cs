using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Persists the stable media version exposed through Sonarr or Radarr.</summary>
public sealed class CompatVersionSelection(SubmarineDbContext db)
{
	public Task<CompatLibraryBinding?> GetForSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
		=> GetOrSelectAsync("sonarr", seriesId, cancellationToken);

	public Task<CompatLibraryBinding?> GetForMovieAsync(int movieId, CancellationToken cancellationToken = default)
		=> GetOrSelectAsync("radarr", movieId, cancellationToken);

	public Task BindSeriesVersionAsync(int seriesId, int mediaVersionId, CancellationToken cancellationToken = default)
		=> BindAsync("sonarr", seriesId, mediaVersionId, cancellationToken);

	public Task BindMovieVersionAsync(int movieId, int mediaVersionId, CancellationToken cancellationToken = default)
		=> BindAsync("radarr", movieId, mediaVersionId, cancellationToken);
	public Task ExcludeSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
		=> SetExcludedAsync("sonarr", seriesId, cancellationToken);

	public Task ExcludeMovieAsync(int movieId, CancellationToken cancellationToken = default)
		=> SetExcludedAsync("radarr", movieId, cancellationToken);

	public Task RebindSeriesBeforeVersionRemovalAsync(int seriesId, int removedVersionId, CancellationToken cancellationToken = default)
		=> RebindBeforeVersionRemovalAsync("sonarr", seriesId, removedVersionId, cancellationToken);

	public Task RebindMovieBeforeVersionRemovalAsync(int movieId, int removedVersionId, CancellationToken cancellationToken = default)
		=> RebindBeforeVersionRemovalAsync("radarr", movieId, removedVersionId, cancellationToken);

	private async Task<CompatLibraryBinding?> GetOrSelectAsync(string facade, int titleId, CancellationToken cancellationToken)
	{
		var binding = await FindAsync(facade, titleId, cancellationToken);
		if (binding?.Excluded == true)
			return null;
		if (binding?.MediaVersionId is not null)
		{
			await ValidateBindingAsync(binding, cancellationToken);
			return binding;
		}

		var versionId = await db.MediaVersions.AsNoTracking()
			.Where(x => facade == "sonarr" ? x.SeriesId == titleId : x.MovieId == titleId)
			.OrderBy(x => x.Id)
			.Select(x => (int?)x.Id)
			.FirstOrDefaultAsync(cancellationToken);
		if (versionId is null)
			return null;
		if (binding is null)
		{
			binding = new CompatLibraryBinding
			{
				Facade = facade,
				SeriesId = facade == "sonarr" ? titleId : null,
				MovieId = facade == "radarr" ? titleId : null
			};
			db.CompatLibraryBindings.Add(binding);
		}
		binding.MediaVersionId = versionId;
		try
		{
			await db.SaveChangesAsync(cancellationToken);
			return binding;
		}
		catch (DbUpdateException)
		{
			db.Entry(binding).State = EntityState.Detached;
			var concurrent = await FindAsync(facade, titleId, cancellationToken);
			if (concurrent is null)
				throw;
			if (concurrent.Excluded)
				return null;
			if (concurrent.MediaVersionId is not null)
				await ValidateBindingAsync(concurrent, cancellationToken);
			return concurrent;
		}
	}

	private async Task BindAsync(string facade, int titleId, int mediaVersionId, CancellationToken cancellationToken)
	{
		var belongs = await db.MediaVersions.AnyAsync(
			x => x.Id == mediaVersionId && (facade == "sonarr" ? x.SeriesId == titleId : x.MovieId == titleId),
			cancellationToken);
		if (!belongs)
			throw new InvalidOperationException($"Media version {mediaVersionId} does not belong to {facade} title {titleId}.");

		var binding = await FindAsync(facade, titleId, cancellationToken);
		if (binding is null)
		{
			binding = new CompatLibraryBinding
			{
				Facade = facade,
				SeriesId = facade == "sonarr" ? titleId : null,
				MovieId = facade == "radarr" ? titleId : null
			};
			db.CompatLibraryBindings.Add(binding);
		}
		binding.MediaVersionId = mediaVersionId;
		binding.Excluded = false;
		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task SetExcludedAsync(string facade, int titleId, CancellationToken cancellationToken)
	{
		var binding = await GetOrSelectAsync(facade, titleId, cancellationToken);
		if (binding is null)
			return;
		binding.Excluded = true;
		binding.MediaVersionId = null;
		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task RebindBeforeVersionRemovalAsync(
		string facade,
		int titleId,
		int removedVersionId,
		CancellationToken cancellationToken)
	{
		var binding = await GetOrSelectAsync(facade, titleId, cancellationToken);
		if (binding is null || binding.MediaVersionId != removedVersionId)
			return;
		var replacement = await db.MediaVersions.AsNoTracking()
			.Where(x => (facade == "sonarr" ? x.SeriesId == titleId : x.MovieId == titleId) && x.Id != removedVersionId)
			.OrderBy(x => x.Id)
			.Select(x => (int?)x.Id)
			.FirstOrDefaultAsync(cancellationToken);
		if (replacement is null)
			throw new InvalidOperationException($"Cannot remove the final {facade} version bound to title {titleId}.");
		binding.MediaVersionId = replacement;
		await db.SaveChangesAsync(cancellationToken);
	}

	private Task<CompatLibraryBinding?> FindAsync(string facade, int titleId, CancellationToken cancellationToken)
		=> db.CompatLibraryBindings.SingleOrDefaultAsync(
			x => x.Facade == facade && (facade == "sonarr" ? x.SeriesId == titleId : x.MovieId == titleId),
			cancellationToken);

	private async Task ValidateBindingAsync(CompatLibraryBinding binding, CancellationToken cancellationToken)
	{
		if (binding.MediaVersionId is not { } versionId)
			throw new InvalidOperationException($"Compatibility binding {binding.Id} has no selected media version.");
		var version = await db.MediaVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
		if (version is null
			|| (binding.Facade == "sonarr" && version.SeriesId != binding.SeriesId)
			|| (binding.Facade == "radarr" && version.MovieId != binding.MovieId))
			throw new InvalidOperationException($"Compatibility binding {binding.Id} refers to a media version that does not belong to its title.");
	}
}

public static class CompatIdentity
{
	public static int LocalId(int nativeId) => nativeId;

	public static void RequireMatchingIds(int routeId, int bodyId)
	{
		if (routeId != bodyId)
			throw new ArgumentException("The URL id must match the body id.", nameof(bodyId));
	}
}
