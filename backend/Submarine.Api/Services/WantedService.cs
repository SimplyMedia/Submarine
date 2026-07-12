using Microsoft.EntityFrameworkCore;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Response;
using Submarine.Core.Library;

namespace Submarine.Api.Services;

/// <summary>
///     Reports monitored media missing a file (wanted) or held below its quality cutoff, and enqueues automatic
///     searches over those sets
/// </summary>
public class WantedService
{
	private const int MaxPageSize = 250;

	private readonly SubmarineDatabaseContext _context;
	private readonly IBackgroundTaskQueue _taskQueue;

	public WantedService(SubmarineDatabaseContext context, IBackgroundTaskQueue taskQueue)
	{
		_context = context;
		_taskQueue = taskQueue;
	}

	public async Task<PagedResult<WantedMissingItem>> GetMissingAsync(int page, int pageSize,
		CancellationToken cancellationToken = default)
	{
		var all = (await ComputeMissingAsync(cancellationToken))
			.OrderBy(i => i.Title)
			.ThenBy(i => i.SeasonNumber)
			.ThenBy(i => i.EpisodeNumber)
			.ToList();

		return Page(all, page, pageSize);
	}

	public async Task<PagedResult<WantedCutoffItem>> GetCutoffAsync(int page, int pageSize,
		CancellationToken cancellationToken = default)
	{
		var all = (await ComputeCutoffAsync(cancellationToken))
			.OrderBy(i => i.Title)
			.ThenBy(i => i.SeasonNumber)
			.ThenBy(i => i.EpisodeNumber)
			.ToList();

		return Page(all, page, pageSize);
	}

	public ValueTask EnqueueMissingSearchAsync()
		=> _taskQueue.QueueAsync(async (sp, ct) =>
		{
			var (episodeIds, movieIds) = await sp.GetRequiredService<WantedService>().GetMissingTargetsAsync(ct);

			await RunSearchAsync(sp, "wanted missing", episodeIds, movieIds, ct);
		});

	public ValueTask EnqueueCutoffSearchAsync()
		=> _taskQueue.QueueAsync(async (sp, ct) =>
		{
			var (episodeIds, movieIds) = await sp.GetRequiredService<WantedService>().GetCutoffTargetsAsync(ct);

			await RunSearchAsync(sp, "cutoff unmet", episodeIds, movieIds, ct);
		});

	private async Task<(IReadOnlyList<int> EpisodeIds, IReadOnlyList<int> MovieIds)> GetMissingTargetsAsync(
		CancellationToken cancellationToken)
	{
		var items = await ComputeMissingAsync(cancellationToken);

		return SplitTargets(items.Select(i => (i.Kind, i.MediaId, i.EpisodeId)));
	}

	private async Task<(IReadOnlyList<int> EpisodeIds, IReadOnlyList<int> MovieIds)> GetCutoffTargetsAsync(
		CancellationToken cancellationToken)
	{
		var items = await ComputeCutoffAsync(cancellationToken);

		return SplitTargets(items.Select(i => (i.Kind, i.MediaId, i.EpisodeId)));
	}

	private static (IReadOnlyList<int> EpisodeIds, IReadOnlyList<int> MovieIds) SplitTargets(
		IEnumerable<(MediaKind Kind, int MediaId, int? EpisodeId)> targets)
	{
		var list = targets.ToList();

		var episodeIds = list.Where(t => t is { Kind: MediaKind.SERIES, EpisodeId: not null })
			.Select(t => t.EpisodeId!.Value)
			.Distinct()
			.ToList();

		var movieIds = list.Where(t => t.Kind == MediaKind.MOVIES)
			.Select(t => t.MediaId)
			.Distinct()
			.ToList();

		return (episodeIds, movieIds);
	}

	private static async Task RunSearchAsync(IServiceProvider sp, string label, IReadOnlyList<int> episodeIds,
		IReadOnlyList<int> movieIds, CancellationToken cancellationToken)
	{
		var auto = sp.GetRequiredService<AutomaticSearchService>();
		var logger = sp.GetRequiredService<ILogger<WantedService>>();

		logger.LogInformation("Starting {Label} search over {Episodes} episodes and {Movies} movies", label,
			episodeIds.Count, movieIds.Count);

		foreach (var episodeId in episodeIds)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await auto.SearchAndGrabEpisodeAsync(episodeId, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "{Label} search for episode {EpisodeId} failed", label, episodeId);
			}
		}

		foreach (var movieId in movieIds)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await auto.SearchAndGrabMovieAsync(movieId, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "{Label} search for movie {MovieId} failed", label, movieId);
			}
		}
	}

	private async Task<List<WantedMissingItem>> ComputeMissingAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;
		var items = new List<WantedMissingItem>();

		var series = await _context.Series.AsNoTracking().Where(s => s.Monitored).ToListAsync(cancellationToken);

		if (series.Count > 0)
		{
			var seriesById = series.ToDictionary(s => s.Id);
			var seriesIds = series.Select(s => s.Id).ToList();

			var versionsBySeries = (await _context.Versions.AsNoTracking()
					.Where(v => v.SeriesId != null)
					.ToListAsync(cancellationToken))
				.Where(v => v.Monitored && seriesById.ContainsKey(v.SeriesId!.Value))
				.GroupBy(v => v.SeriesId!.Value)
				.ToDictionary(g => g.Key, g => g.ToList());

			var episodes = (await _context.Episodes.AsNoTracking()
					.Where(e => seriesIds.Contains(e.SeriesId) && e.Monitored && e.AirDate != null)
					.ToListAsync(cancellationToken))
				.Where(e => e.AirDate <= now)
				.ToList();

			var present = new HashSet<(int EpisodeId, int VersionId)>();

			foreach (var file in await _context.EpisodeFiles.AsNoTracking().Include(f => f.Episodes)
				         .Where(f => seriesIds.Contains(f.SeriesId)).ToListAsync(cancellationToken))
			foreach (var episode in file.Episodes)
				present.Add((episode.Id, file.MediaVersionId));

			foreach (var episode in episodes)
			{
				if (!versionsBySeries.TryGetValue(episode.SeriesId, out var versions))
					continue;

				var missing = versions.Where(v => !present.Contains((episode.Id, v.Id))).Select(v => v.Name).ToList();

				if (missing.Count == 0)
					continue;

				items.Add(new WantedMissingItem(MediaKind.SERIES, episode.SeriesId, episode.Id,
					seriesById[episode.SeriesId].Title, episode.SeasonNumber, episode.EpisodeNumber, missing));
			}
		}

		var movies = (await _context.Movies.AsNoTracking().Where(m => m.Monitored).ToListAsync(cancellationToken))
			.Where(m => m.ReleaseDate != null && m.ReleaseDate <= now)
			.ToList();

		if (movies.Count > 0)
		{
			var movieIds = movies.Select(m => m.Id).ToList();

			var versionsByMovie = (await _context.Versions.AsNoTracking()
					.Where(v => v.MovieId != null)
					.ToListAsync(cancellationToken))
				.Where(v => v.Monitored && movieIds.Contains(v.MovieId!.Value))
				.GroupBy(v => v.MovieId!.Value)
				.ToDictionary(g => g.Key, g => g.ToList());

			var present = (await _context.MovieFiles.AsNoTracking()
					.Where(f => movieIds.Contains(f.MovieId))
					.ToListAsync(cancellationToken))
				.Select(f => (f.MovieId, f.MediaVersionId))
				.ToHashSet();

			foreach (var movie in movies)
			{
				if (!versionsByMovie.TryGetValue(movie.Id, out var versions))
					continue;

				var missing = versions.Where(v => !present.Contains((movie.Id, v.Id))).Select(v => v.Name).ToList();

				if (missing.Count == 0)
					continue;

				items.Add(new WantedMissingItem(MediaKind.MOVIES, movie.Id, null, movie.Title, null, null, missing));
			}
		}

		return items;
	}

	private async Task<List<WantedCutoffItem>> ComputeCutoffAsync(CancellationToken cancellationToken)
	{
		var profiles = (await _context.QualityProfiles.AsNoTracking().ToListAsync(cancellationToken))
			.ToDictionary(p => p.Id);
		var versions = (await _context.Versions.AsNoTracking().ToListAsync(cancellationToken))
			.ToDictionary(v => v.Id);
		var seriesById = (await _context.Series.AsNoTracking().ToListAsync(cancellationToken))
			.ToDictionary(s => s.Id);
		var moviesById = (await _context.Movies.AsNoTracking().ToListAsync(cancellationToken))
			.ToDictionary(m => m.Id);

		var items = new List<WantedCutoffItem>();

		foreach (var file in await _context.EpisodeFiles.AsNoTracking().Include(f => f.Episodes)
			         .ToListAsync(cancellationToken))
		{
			if (!versions.TryGetValue(file.MediaVersionId, out var version) || !version.Monitored)
				continue;

			if (!profiles.TryGetValue(version.QualityProfileId, out var profile) || profile.MeetsCutoff(file.Quality))
				continue;

			if (!seriesById.TryGetValue(file.SeriesId, out var series))
				continue;

			var episode = file.Episodes.FirstOrDefault();

			items.Add(new WantedCutoffItem(MediaKind.SERIES, file.SeriesId, episode?.Id, series.Title,
				episode?.SeasonNumber, episode?.EpisodeNumber, version.Name, file.Quality));
		}

		foreach (var file in await _context.MovieFiles.AsNoTracking().ToListAsync(cancellationToken))
		{
			if (!versions.TryGetValue(file.MediaVersionId, out var version) || !version.Monitored)
				continue;

			if (!profiles.TryGetValue(version.QualityProfileId, out var profile) || profile.MeetsCutoff(file.Quality))
				continue;

			if (!moviesById.TryGetValue(file.MovieId, out var movie))
				continue;

			items.Add(new WantedCutoffItem(MediaKind.MOVIES, file.MovieId, null, movie.Title, null, null, version.Name,
				file.Quality));
		}

		return items;
	}

	private static PagedResult<T> Page<T>(IReadOnlyList<T> all, int page, int pageSize)
	{
		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

		var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

		return new PagedResult<T>(items, page, pageSize, all.Count);
	}
}
