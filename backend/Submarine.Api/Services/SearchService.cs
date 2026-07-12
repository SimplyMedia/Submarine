using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.DecisionEngine;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Indexer;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.Parser;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;

namespace Submarine.Api.Services;

public class SearchService
{
	private readonly ILogger<SearchService> _logger;
	private readonly IProviderRepository _providerRepository;
	private readonly ISeriesRepository _seriesRepository;
	private readonly IMovieRepository _movieRepository;
	private readonly IQualityProfileRepository _qualityProfileRepository;
	private readonly ILanguageProfileRepository _languageProfileRepository;
	private readonly IReleaseFilterRepository _filterRepository;
	private readonly ICustomFormatRepository _formatRepository;
	private readonly ITorznabSearchClient _torznabHttpClient;
	private readonly IMappingsClient _mappingsClient;
	private readonly IParser<BaseRelease> _releaseParser;
	private readonly DownloadDecisionService _decisionService;

	public SearchService(ILogger<SearchService> logger, IProviderRepository providerRepository,
		ISeriesRepository seriesRepository, IMovieRepository movieRepository,
		IQualityProfileRepository qualityProfileRepository, ILanguageProfileRepository languageProfileRepository,
		IReleaseFilterRepository filterRepository, ICustomFormatRepository formatRepository,
		ITorznabSearchClient torznabHttpClient, IMappingsClient mappingsClient, IParser<BaseRelease> releaseParser,
		DownloadDecisionService decisionService)
	{
		_logger = logger;
		_providerRepository = providerRepository;
		_seriesRepository = seriesRepository;
		_movieRepository = movieRepository;
		_qualityProfileRepository = qualityProfileRepository;
		_languageProfileRepository = languageProfileRepository;
		_filterRepository = filterRepository;
		_formatRepository = formatRepository;
		_torznabHttpClient = torznabHttpClient;
		_mappingsClient = mappingsClient;
		_releaseParser = releaseParser;
		_decisionService = decisionService;
	}

	public async Task<IReadOnlyList<VersionedDownloadDecision>> SearchEpisodeAsync(int seriesId, int season,
		int episode, CancellationToken cancellationToken = default)
	{
		var series = await GetSeriesAsync(seriesId);

		var target = await _seriesRepository.QueryEpisodes(seriesId)
			.FirstOrDefaultAsync(e => e.SeasonNumber == season && e.EpisodeNumber == episode, cancellationToken);

		if (target == null)
			throw new NotFoundException();

		return await SearchForEpisodeAsync(series, target, cancellationToken);
	}

	public async Task<IReadOnlyList<VersionedDownloadDecision>> SearchEpisodeByIdAsync(int episodeId,
		CancellationToken cancellationToken = default)
	{
		var target = await _seriesRepository.FindEpisodeAsync(episodeId);

		if (target == null)
			throw new NotFoundException();

		return await SearchForEpisodeAsync(await GetSeriesAsync(target.SeriesId), target, cancellationToken);
	}

	public async Task<IReadOnlyList<VersionedDownloadDecision>> SearchSeasonAsync(int seriesId, int season,
		CancellationToken cancellationToken = default)
	{
		var series = await GetSeriesAsync(seriesId);

		var scene = ResolveSceneVariant(await TryGetSceneMappingsAsync(series.TvdbId, cancellationToken), series,
			season, episode: null);

		var candidates = await FetchCandidatesAsync(protocol: null, cancellationToken, async indexer =>
		{
			var releases = new List<ReleaseInfo>(await _torznabHttpClient.TvSearchAsync(indexer, series.TvdbId, season,
				categories: SeriesCategories(indexer, series.Type), cancellationToken: cancellationToken));

			if (scene != null)
				releases.AddRange(await _torznabHttpClient.TvSearchAsync(indexer, series.TvdbId, scene.Season,
					query: scene.Title != series.Title ? scene.Title : null,
					categories: SeriesCategories(indexer, series.Type), cancellationToken: cancellationToken));

			return releases.DistinctBy(release => release.Guid ?? release.DownloadUrl).ToList();
		});

		var filters = await LoadFiltersAsync();
		var formats = await LoadFormatsAsync();
		var results = new List<VersionedDownloadDecision>();

		foreach (var version in await LoadMonitoredSeriesVersionsAsync(series.Id))
		{
			var context = await BuildSeriesContextAsync(version, series.Id, season, existingFileQuality: null,
				existingFileLanguages: null, filters, formats);

			AddDecisions(results, candidates, context, version);
		}

		return results;
	}

	public async Task<IReadOnlyList<VersionedDownloadDecision>> SearchMovieAsync(int movieId,
		CancellationToken cancellationToken = default)
	{
		var movie = await _movieRepository.FirstByConditionAsync(m => m.Id == movieId);

		if (movie == null)
			throw new NotFoundException();

		var candidates = await FetchCandidatesAsync(protocol: null, cancellationToken, indexer =>
			_torznabHttpClient.MovieSearchAsync(indexer, movie.TmdbId, movie.ImdbId,
				categories: Categories(indexer), cancellationToken: cancellationToken));

		var filters = await LoadFiltersAsync();
		var formats = await LoadFormatsAsync();
		var results = new List<VersionedDownloadDecision>();

		foreach (var version in (await _movieRepository.FindVersionsAsync(movie.Id)).Where(v => v.Monitored))
		{
			var existingFile = await _movieRepository.FindMovieFileForVersionAsync(movie.Id, version.Id);
			var qualityProfile = await GetQualityProfileAsync(version.QualityProfileId);

			var context = new MediaContext
			{
				QualityProfile = qualityProfile,
				LanguageProfile = await GetLanguageProfileAsync(version.LanguageProfileId),
				Filters = filters,
				CustomFormats = formats,
				CustomFormatScores = qualityProfile.FormatScores,
				ExistingFileQuality = existingFile?.Quality,
				ExistingFileLanguages = existingFile?.Languages
			};

			AddDecisions(results, candidates, context, version);
		}

		return results;
	}

	public async Task<IReadOnlyList<VersionedDownloadDecision>> SearchTermAsync(string term, Protocol? protocol,
		CancellationToken cancellationToken = default)
	{
		var qualityProfile = await _qualityProfileRepository.Query().OrderBy(p => p.Id)
			.FirstOrDefaultAsync(cancellationToken);
		var languageProfile = await _languageProfileRepository.Query().OrderBy(p => p.Id)
			.FirstOrDefaultAsync(cancellationToken);

		if (qualityProfile == null || languageProfile == null)
			return Array.Empty<VersionedDownloadDecision>();

		var context = new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = languageProfile,
			Filters = await LoadFiltersAsync(),
			CustomFormats = await LoadFormatsAsync(),
			CustomFormatScores = qualityProfile.FormatScores
		};

		var candidates = await FetchCandidatesAsync(protocol, cancellationToken, indexer =>
			_torznabHttpClient.SearchAsync(indexer, term, Categories(indexer), cancellationToken));

		return _decisionService.DecideAll(candidates, context)
			.Select(decision => new VersionedDownloadDecision(decision, null, null))
			.ToList();
	}

	private async Task<IReadOnlyList<VersionedDownloadDecision>> SearchForEpisodeAsync(Series series, Episode episode,
		CancellationToken cancellationToken)
	{
		var scene = ResolveSceneVariant(await TryGetSceneMappingsAsync(series.TvdbId, cancellationToken), series,
			episode.SeasonNumber, episode.EpisodeNumber);

		var aniList = series.Type == SeriesType.ANIME
			? await TryResolveAniListAsync(series.TvdbId, episode.SeasonNumber, episode.EpisodeNumber, cancellationToken)
			: null;

		var candidates = await FetchCandidatesAsync(protocol: null, cancellationToken, async indexer =>
		{
			var releases = new List<ReleaseInfo>(await _torznabHttpClient.TvSearchAsync(indexer, series.TvdbId,
				episode.SeasonNumber, episode.EpisodeNumber, categories: SeriesCategories(indexer, series.Type),
				cancellationToken: cancellationToken));

			if (scene != null)
				releases.AddRange(await _torznabHttpClient.TvSearchAsync(indexer, series.TvdbId, scene.Season,
					scene.Episode, query: scene.Title != series.Title ? scene.Title : null,
					categories: SeriesCategories(indexer, series.Type), cancellationToken: cancellationToken));

			if (series.Type == SeriesType.ANIME && episode.AbsoluteEpisodeNumber is { } absolute)
				releases.AddRange(await _torznabHttpClient.SearchAsync(indexer,
					$"{series.Title} {absolute:00}", AnimeCategories(indexer), cancellationToken));

			if (aniList != null)
				releases.AddRange(await _torznabHttpClient.SearchAsync(indexer,
					$"{scene?.Title ?? series.Title} {aniList.AniListEpisode:00}", AnimeCategories(indexer),
					cancellationToken));

			return releases.DistinctBy(release => release.Guid ?? release.DownloadUrl).ToList();
		});

		var filters = await LoadFiltersAsync();
		var formats = await LoadFormatsAsync();
		var results = new List<VersionedDownloadDecision>();

		foreach (var version in await LoadMonitoredSeriesVersionsAsync(series.Id))
		{
			var existingFile = await _seriesRepository.FindEpisodeFileForVersionAsync(episode.Id, version.Id);

			var context = await BuildSeriesContextAsync(version, series.Id, episode.SeasonNumber,
				existingFile?.Quality, existingFile?.Languages, filters, formats);

			AddDecisions(results, candidates, context, version);
		}

		return results;
	}

	private async Task<List<MediaVersion>> LoadMonitoredSeriesVersionsAsync(int seriesId)
		=> (await _seriesRepository.FindVersionsAsync(seriesId)).Where(v => v.Monitored).ToList();

	private void AddDecisions(List<VersionedDownloadDecision> results,
		IReadOnlyCollection<ReleaseCandidate> candidates, MediaContext context, MediaVersion version)
	{
		foreach (var decision in _decisionService.DecideAll(candidates, context))
			results.Add(new VersionedDownloadDecision(decision, version.Id, version.Name));
	}

	private async Task<SceneMappingSet?> TryGetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken)
	{
		try
		{
			return await _mappingsClient.GetSceneMappingsAsync(tvdbId, cancellationToken);
		}
		catch (Exception ex)
		{
			if (cancellationToken.IsCancellationRequested)
				throw;

			_logger.LogDebug(ex, "Fetching scene mappings for series {TvdbId} failed", tvdbId);
			return null;
		}
	}

	private async Task<AniListResolution?> TryResolveAniListAsync(int tvdbId, int season, int episode,
		CancellationToken cancellationToken)
	{
		try
		{
			return await _mappingsClient.ResolveAniListAsync(tvdbId, season, episode, cancellationToken);
		}
		catch (Exception ex)
		{
			if (cancellationToken.IsCancellationRequested)
				throw;

			_logger.LogDebug(ex, "Resolving AniList numbering for series {TvdbId} S{Season}E{Episode} failed", tvdbId,
				season, episode);
			return null;
		}
	}

	private static SceneVariant? ResolveSceneVariant(SceneMappingSet? set, Series series, int season, int? episode)
	{
		if (set == null)
			return null;

		var seasonMapping = set.Mappings
			.Where(m => m.SeasonNumber == season || m.SeasonNumber == null)
			.OrderByDescending(m => m.SeasonNumber != null)
			.FirstOrDefault();

		if (episode is { } ep)
		{
			var episodeOverride = set.EpisodeMappings
				.FirstOrDefault(m => m.SeasonNumber == season && m.EpisodeNumber == ep);

			if (episodeOverride != null)
				return new SceneVariant(episodeOverride.SceneSeasonNumber, episodeOverride.SceneEpisodeNumber,
					seasonMapping?.Title ?? series.Title);
		}

		if (seasonMapping == null)
			return null;

		return new SceneVariant(seasonMapping.SceneSeasonNumber ?? season,
			episode is { } e ? e + seasonMapping.EpisodeOffset : null, seasonMapping.Title);
	}

	private sealed record SceneVariant(int Season, int? Episode, string Title);

	private async Task<MediaContext> BuildSeriesContextAsync(MediaVersion version, int seriesId, int season,
		QualityModel? existingFileQuality, IReadOnlyList<Language>? existingFileLanguages,
		IReadOnlyCollection<ReleaseFilter> filters, IReadOnlyCollection<CustomFormat> formats)
	{
		var qualityProfile = await GetQualityProfileAsync(version.QualityProfileId);

		var seasonFiles = await _seriesRepository.FindEpisodeFilesBySeasonAsync(seriesId, season, version.Id);
		var seasonReleaseGroup = seasonFiles
			.Select(f => f.ReleaseGroup)
			.Where(group => group != null)
			.GroupBy(group => group)
			.OrderByDescending(group => group.Count())
			.Select(group => group.Key)
			.FirstOrDefault();

		return new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = await GetLanguageProfileAsync(version.LanguageProfileId),
			Filters = filters,
			CustomFormats = formats,
			CustomFormatScores = qualityProfile.FormatScores,
			ExistingFileQuality = existingFileQuality,
			ExistingFileLanguages = existingFileLanguages,
			SeasonReleaseGroup = seasonReleaseGroup
		};
	}

	private async Task<List<ReleaseCandidate>> FetchCandidatesAsync(Protocol? protocol,
		CancellationToken cancellationToken, Func<Provider, Task<IReadOnlyList<ReleaseInfo>>> fetch)
	{
		var indexers = await LoadIndexersAsync(protocol, cancellationToken);

		return (await Task.WhenAll(indexers.Select(indexer => FetchCandidatesAsync(indexer, fetch, cancellationToken))))
			.SelectMany(candidate => candidate)
			.ToList();
	}

	private async Task<IReadOnlyList<ReleaseCandidate>> FetchCandidatesAsync(Provider indexer,
		Func<Provider, Task<IReadOnlyList<ReleaseInfo>>> fetch, CancellationToken cancellationToken)
	{
		try
		{
			var candidates = new List<ReleaseCandidate>();

			foreach (var info in await fetch(indexer))
			{
				try
				{
					var parsed = _releaseParser.Parse(info.Title);
					candidates.Add(new ReleaseCandidate(parsed, info, indexer.Name, indexer.Priority,
						(indexer as TorznabIndexer)?.MinimumSeeders));
				}
				catch (NotParsableReleaseException ex)
				{
					_logger.LogDebug(ex, "Skipping unparsable release {Title} from {Indexer}", info.Title, indexer.Name);
				}
			}

			return candidates;
		}
		catch (Exception ex)
		{
			if (cancellationToken.IsCancellationRequested)
				throw;

			_logger.LogWarning(ex, "Search on indexer {Indexer} failed", indexer.Name);
			return Array.Empty<ReleaseCandidate>();
		}
	}

	private async Task<List<Provider>> LoadIndexersAsync(Protocol? protocol, CancellationToken cancellationToken)
	{
		var indexers = await _providerRepository.Query()
			.Where(p => p is TorznabIndexer || p is NewznabIndexer)
			.ToListAsync(cancellationToken);

		return indexers
			.Where(p => p.Mode.HasFlag(ProviderMode.AUTOMATIC_SEARCH) || p.Mode.HasFlag(ProviderMode.MANUAL_SEARCH))
			.Where(p => protocol == null || p.Protocol == protocol)
			.ToList();
	}

	private async Task<Series> GetSeriesAsync(int seriesId)
	{
		var series = await _seriesRepository.FirstByConditionAsync(s => s.Id == seriesId);

		if (series == null)
			throw new NotFoundException();

		return series;
	}

	private async Task<QualityProfile> GetQualityProfileAsync(int id)
	{
		var profile = await _qualityProfileRepository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}

	private async Task<LanguageProfile> GetLanguageProfileAsync(int id)
	{
		var profile = await _languageProfileRepository.FirstByConditionAsync(p => p.Id == id);

		if (profile == null)
			throw new NotFoundException();

		return profile;
	}

	private async Task<IReadOnlyCollection<Core.DecisionEngine.Filter.ReleaseFilter>> LoadFiltersAsync()
		=> (await _filterRepository.FindAllAsync()).Select(f => f.ToFilter()).ToList();

	private async Task<IReadOnlyCollection<Core.DecisionEngine.CustomFormats.CustomFormat>> LoadFormatsAsync()
		=> (await _formatRepository.FindAllAsync()).Select(f => f.ToFormat()).ToList();

	private static IReadOnlyList<int> Categories(Provider indexer)
		=> indexer switch
		{
			TorznabIndexer torznab => torznab.Categories,
			NewznabIndexer newznab => newznab.Categories,
			_ => Array.Empty<int>()
		};

	private static IReadOnlyList<int> AnimeCategories(Provider indexer)
		=> indexer switch
		{
			TorznabIndexer torznab => torznab.AnimeCategories,
			NewznabIndexer newznab => newznab.AnimeCategories,
			_ => Array.Empty<int>()
		};

	private static IReadOnlyList<int> SeriesCategories(Provider indexer, SeriesType type)
		=> type == SeriesType.ANIME ? AnimeCategories(indexer) : Categories(indexer);
}
