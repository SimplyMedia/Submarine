using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Indexer;
using Submarine.Core.Library;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;

namespace Submarine.Api.Jobs;

/// <summary>
///     Periodically pulls the recent releases of RSS-enabled indexers, matches them to the library and auto-grabs
///     approved releases. The interval is driven by <see cref="IndexerConfig.RssSyncIntervalMinutes" />, re-read every run.
/// </summary>
public sealed class RssSyncJob : IScheduledJob
{
	private const int DefaultIntervalMinutes = 30;

	private volatile int _intervalMinutes = DefaultIntervalMinutes;

	public string Name => "RssSync";

	/// <summary>
	///     Interval until the next run, reflecting the last observed config value. A disabled config falls back to the
	///     default so the config keeps being polled.
	/// </summary>
	public TimeSpan Interval => TimeSpan.FromMinutes(_intervalMinutes > 0 ? _intervalMinutes : DefaultIntervalMinutes);

	public async Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		var logger = scopedProvider.GetRequiredService<ILogger<RssSyncJob>>();
		var config = await scopedProvider.GetRequiredService<SettingsService>().GetIndexerConfigAsync();

		_intervalMinutes = config.RssSyncIntervalMinutes;

		if (config.RssSyncIntervalMinutes <= 0)
		{
			logger.LogDebug("RSS sync disabled, interval is {Interval} minutes", config.RssSyncIntervalMinutes);
			return;
		}

		var context = scopedProvider.GetRequiredService<SubmarineDatabaseContext>();

		var indexers = (await context.Providers.AsNoTracking().ToListAsync(cancellationToken))
			.Where(p => p is TorznabIndexer or NewznabIndexer && p.Mode.HasFlag(ProviderMode.RSS))
			.ToList();

		if (indexers.Count == 0)
			return;

		var series = await context.Series.AsNoTracking().ToListAsync(cancellationToken);
		var movies = await context.Movies.AsNoTracking().ToListAsync(cancellationToken);
		var trackedTitles = (await context.TrackedDownloads.AsNoTracking()
				.Select(t => t.ReleaseTitle)
				.ToListAsync(cancellationToken))
			.ToHashSet();

		var run = new Run(context, config, logger,
			scopedProvider.GetRequiredService<ITorznabSearchClient>(),
			scopedProvider.GetRequiredService<IParser<BaseRelease>>(),
			scopedProvider.GetRequiredService<MediaContextFactory>(),
			scopedProvider.GetRequiredService<DownloadDecisionService>(),
			scopedProvider.GetRequiredService<GrabService>(),
			scopedProvider.GetRequiredService<BlocklistService>(),
			new ReleaseMediaMatcher(series, movies), trackedTitles);

		foreach (var indexer in indexers)
		{
			try
			{
				var releases = await run.SearchClient.RecentAsync(indexer, Categories(indexer),
					cancellationToken: cancellationToken);

				foreach (var info in releases)
					try
					{
						await run.ProcessReleaseAsync(info, indexer, cancellationToken);
					}
					catch (Exception ex)
					{
						if (cancellationToken.IsCancellationRequested)
							throw;

						logger.LogWarning(ex, "Processing RSS release {Title} from {Indexer} failed", info.Title,
							indexer.Name);
					}
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
					throw;

				logger.LogWarning(ex, "RSS sync on indexer {Indexer} failed", indexer.Name);
			}
		}
	}

	private static IReadOnlyList<int> Categories(Provider indexer)
		=> indexer switch
		{
			TorznabIndexer torznab => torznab.Categories,
			NewznabIndexer newznab => newznab.Categories,
			_ => Array.Empty<int>()
		};

	/// <summary>
	///     Holds the resolved services and per-run state while processing a single RSS sync run
	/// </summary>
	private sealed class Run
	{
		private readonly SubmarineDatabaseContext _context;
		private readonly IndexerConfig _config;
		private readonly ILogger _logger;
		private readonly IParser<BaseRelease> _parser;
		private readonly MediaContextFactory _contextFactory;
		private readonly DownloadDecisionService _decisionService;
		private readonly GrabService _grabService;
		private readonly BlocklistService _blocklistService;
		private readonly ReleaseMediaMatcher _matcher;
		private readonly HashSet<string> _trackedTitles;
		private readonly HashSet<string> _grabbedGuids = new();

		public ITorznabSearchClient SearchClient { get; }

		public Run(SubmarineDatabaseContext context, IndexerConfig config, ILogger logger,
			ITorznabSearchClient searchClient, IParser<BaseRelease> parser, MediaContextFactory contextFactory,
			DownloadDecisionService decisionService, GrabService grabService, BlocklistService blocklistService,
			ReleaseMediaMatcher matcher, HashSet<string> trackedTitles)
		{
			_context = context;
			_config = config;
			_logger = logger;
			SearchClient = searchClient;
			_parser = parser;
			_contextFactory = contextFactory;
			_decisionService = decisionService;
			_grabService = grabService;
			_blocklistService = blocklistService;
			_matcher = matcher;
			_trackedTitles = trackedTitles;
		}

		public async Task ProcessReleaseAsync(ReleaseInfo info, Provider indexer, CancellationToken cancellationToken)
		{
			if (_grabbedGuids.Contains(info.Guid) || _trackedTitles.Contains(info.Title))
				return;

			if (!PassesGates(info, indexer))
				return;

			BaseRelease parsed;

			try
			{
				parsed = _parser.Parse(info.Title);
			}
			catch (NotParsableReleaseException ex)
			{
				_logger.LogDebug(ex, "Skipping unparsable RSS release {Title} from {Indexer}", info.Title, indexer.Name);
				return;
			}

			if (parsed.SeriesReleaseData != null)
				await ProcessSeriesAsync(parsed, info, indexer, cancellationToken);
			else if (parsed.MovieReleaseData != null)
				await ProcessMovieAsync(parsed, info, indexer, cancellationToken);
		}

		private bool PassesGates(ReleaseInfo info, Provider indexer)
		{
			var now = DateTimeOffset.UtcNow;

			if (indexer.Protocol == Protocol.USENET && _config.MinimumAgeMinutes > 0 && info.PublishDate is { } published
			    && (now - published).TotalMinutes < _config.MinimumAgeMinutes)
				return false;

			if (_config.RetentionDays > 0 && info.PublishDate is { } age
			    && (now - age).TotalDays > _config.RetentionDays)
				return false;

			if (_config.MaximumSizeMb > 0 && info.Size is { } size && size > (long)_config.MaximumSizeMb * 1024 * 1024)
				return false;

			return true;
		}

		private async Task ProcessSeriesAsync(BaseRelease parsed, ReleaseInfo info, Provider indexer,
			CancellationToken cancellationToken)
		{
			var series = _matcher.MatchSeries(parsed, info.TvdbId);

			if (series == null)
				return;

			var data = parsed.SeriesReleaseData!;
			var season = data.Seasons.Count > 0 ? data.Seasons[0] : -1;
			var isPack = data.ReleaseType is SeriesReleaseType.FULL_SEASON or SeriesReleaseType.PARTIAL_SEASON
				or SeriesReleaseType.MULTI_SEASON;

			var seasonEpisodes = await _context.Episodes.AsNoTracking()
				.Where(e => e.SeriesId == series.Id && e.SeasonNumber == season)
				.ToListAsync(cancellationToken);

			var targets = isPack
				? seasonEpisodes.Where(e => e.Monitored).ToList()
				: seasonEpisodes.Where(e => e.Monitored && data.Episodes.Contains(e.EpisodeNumber)).ToList();

			if (targets.Count == 0)
				return;

			var versions = (await _context.Versions.AsNoTracking()
					.Where(v => v.SeriesId == series.Id)
					.ToListAsync(cancellationToken))
				.Where(v => v.Monitored)
				.ToList();

			var candidate = Candidate(parsed, info, indexer);
			var filters = await _contextFactory.LoadFiltersAsync();
			var formats = await _contextFactory.LoadFormatsAsync();
			var episodeIds = targets.Select(e => e.Id).ToList();

			foreach (var version in versions)
			{
				var existing = targets.Count == 1 && !isPack
					? await _context.EpisodeFiles.AsNoTracking()
						.FirstOrDefaultAsync(f => f.MediaVersionId == version.Id
						                          && f.Episodes.Any(e => e.Id == targets[0].Id), cancellationToken)
					: null;

				var mediaContext = await _contextFactory.BuildSeriesContextAsync(version, series.Id, season,
					existing?.Quality, existing?.Languages, filters, formats);

				if (await TryGrabAsync(candidate, mediaContext, info, indexer, series.Id, episodeIds, movieId: null,
					    version.Id, cancellationToken))
					return;
			}
		}

		private async Task ProcessMovieAsync(BaseRelease parsed, ReleaseInfo info, Provider indexer,
			CancellationToken cancellationToken)
		{
			var movie = _matcher.MatchMovie(parsed);

			if (movie is not { Monitored: true })
				return;

			var versions = (await _context.Versions.AsNoTracking()
					.Where(v => v.MovieId == movie.Id)
					.ToListAsync(cancellationToken))
				.Where(v => v.Monitored)
				.ToList();

			var candidate = Candidate(parsed, info, indexer);
			var filters = await _contextFactory.LoadFiltersAsync();
			var formats = await _contextFactory.LoadFormatsAsync();

			foreach (var version in versions)
			{
				var existing = await _context.MovieFiles.AsNoTracking()
					.FirstOrDefaultAsync(f => f.MovieId == movie.Id && f.MediaVersionId == version.Id,
						cancellationToken);

				var mediaContext = await _contextFactory.BuildMovieContextAsync(version, existing?.Quality,
					existing?.Languages, filters, formats);

				if (await TryGrabAsync(candidate, mediaContext, info, indexer, seriesId: null, episodeIds: null,
					    movie.Id, version.Id, cancellationToken))
					return;
			}
		}

		private async Task<bool> TryGrabAsync(ReleaseCandidate candidate, MediaContext mediaContext, ReleaseInfo info,
			Provider indexer, int? seriesId, IReadOnlyList<int>? episodeIds, int? movieId, int versionId,
			CancellationToken cancellationToken)
		{
			var decision = _decisionService.DecideAll(new[] { candidate }, mediaContext).FirstOrDefault();

			if (decision is not { Approved: true })
				return false;

			if (await _blocklistService.IsBlockedAsync(info.Guid, info.Title))
				return false;

			await _grabService.GrabAsync(new GrabReleaseRequest(info.Title, info.Guid, info.DownloadUrl, indexer.Name,
				info.Protocol, info.Size, seriesId, episodeIds, movieId, versionId), cancellationToken);

			_grabbedGuids.Add(info.Guid);
			_trackedTitles.Add(info.Title);

			return true;
		}

		private static ReleaseCandidate Candidate(BaseRelease parsed, ReleaseInfo info, Provider indexer)
			=> new(parsed, info, indexer.Name, indexer.Priority, (indexer as TorznabIndexer)?.MinimumSeeders);
	}
}
