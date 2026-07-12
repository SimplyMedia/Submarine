using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Repository;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Indexer;
using Submarine.Core.Library;
using Submarine.Core.Parser;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
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
	private readonly TorznabHttpClient _torznabHttpClient;
	private readonly IParser<BaseRelease> _releaseParser;
	private readonly DownloadDecisionService _decisionService;

	public SearchService(ILogger<SearchService> logger, IProviderRepository providerRepository,
		ISeriesRepository seriesRepository, IMovieRepository movieRepository,
		IQualityProfileRepository qualityProfileRepository, ILanguageProfileRepository languageProfileRepository,
		IReleaseFilterRepository filterRepository, ICustomFormatRepository formatRepository,
		TorznabHttpClient torznabHttpClient, IParser<BaseRelease> releaseParser,
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
		_releaseParser = releaseParser;
		_decisionService = decisionService;
	}

	public async Task<IReadOnlyList<DownloadDecision>> SearchEpisodeAsync(int seriesId, int season, int episode,
		CancellationToken cancellationToken = default)
	{
		var series = await GetSeriesAsync(seriesId);

		var target = await _seriesRepository.QueryEpisodes(seriesId)
			.FirstOrDefaultAsync(e => e.SeasonNumber == season && e.EpisodeNumber == episode, cancellationToken);

		if (target == null)
			throw new NotFoundException();

		return await SearchForEpisodeAsync(series, target, cancellationToken);
	}

	public async Task<IReadOnlyList<DownloadDecision>> SearchEpisodeByIdAsync(int episodeId,
		CancellationToken cancellationToken = default)
	{
		var target = await _seriesRepository.FindEpisodeAsync(episodeId);

		if (target == null)
			throw new NotFoundException();

		return await SearchForEpisodeAsync(await GetSeriesAsync(target.SeriesId), target, cancellationToken);
	}

	public async Task<IReadOnlyList<DownloadDecision>> SearchSeasonAsync(int seriesId, int season,
		CancellationToken cancellationToken = default)
	{
		var series = await GetSeriesAsync(seriesId);

		var context = await BuildSeriesContextAsync(series, season, existingFileQuality: null,
			existingFileLanguages: null, cancellationToken);

		return await SearchAsync(protocol: null, cancellationToken, indexer =>
			_torznabHttpClient.TvSearchAsync(indexer, series.TvdbId, season,
				categories: SeriesCategories(indexer, series.Type), cancellationToken: cancellationToken),
			context);
	}

	public async Task<IReadOnlyList<DownloadDecision>> SearchMovieAsync(int movieId,
		CancellationToken cancellationToken = default)
	{
		var movie = await _movieRepository.FirstByConditionAsync(m => m.Id == movieId);

		if (movie == null)
			throw new NotFoundException();

		var existingFile = movie.MovieFileId != null
			? await _movieRepository.FindMovieFileAsync(movie.MovieFileId.Value)
			: null;

		var qualityProfile = await GetQualityProfileAsync(movie.QualityProfileId);

		var context = new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = await GetLanguageProfileAsync(movie.LanguageProfileId),
			Filters = await LoadFiltersAsync(),
			CustomFormats = await LoadFormatsAsync(),
			CustomFormatScores = qualityProfile.FormatScores,
			ExistingFileQuality = existingFile?.Quality,
			ExistingFileLanguages = existingFile?.Languages
		};

		return await SearchAsync(protocol: null, cancellationToken, indexer =>
			_torznabHttpClient.MovieSearchAsync(indexer, movie.TmdbId, movie.ImdbId,
				categories: Categories(indexer), cancellationToken: cancellationToken), context);
	}

	public async Task<IReadOnlyList<DownloadDecision>> SearchTermAsync(string term, Protocol? protocol,
		CancellationToken cancellationToken = default)
	{
		var qualityProfile = await _qualityProfileRepository.Query().OrderBy(p => p.Id)
			.FirstOrDefaultAsync(cancellationToken);
		var languageProfile = await _languageProfileRepository.Query().OrderBy(p => p.Id)
			.FirstOrDefaultAsync(cancellationToken);

		if (qualityProfile == null || languageProfile == null)
			return Array.Empty<DownloadDecision>();

		var context = new MediaContext
		{
			QualityProfile = qualityProfile,
			LanguageProfile = languageProfile,
			Filters = await LoadFiltersAsync(),
			CustomFormats = await LoadFormatsAsync(),
			CustomFormatScores = qualityProfile.FormatScores
		};

		return await SearchAsync(protocol, cancellationToken, indexer =>
			_torznabHttpClient.SearchAsync(indexer, term, Categories(indexer), cancellationToken), context);
	}

	private async Task<IReadOnlyList<DownloadDecision>> SearchForEpisodeAsync(Series series, Episode episode,
		CancellationToken cancellationToken)
	{
		var existingFile = episode.EpisodeFileId != null
			? await _seriesRepository.FindEpisodeFileAsync(episode.EpisodeFileId.Value)
			: null;

		var context = await BuildSeriesContextAsync(series, episode.SeasonNumber, existingFile?.Quality,
			existingFile?.Languages, cancellationToken);

		return await SearchAsync(protocol: null, cancellationToken, async indexer =>
		{
			var releases = new List<ReleaseInfo>(await _torznabHttpClient.TvSearchAsync(indexer, series.TvdbId,
				episode.SeasonNumber, episode.EpisodeNumber, categories: SeriesCategories(indexer, series.Type),
				cancellationToken: cancellationToken));

			if (series.Type == SeriesType.ANIME && episode.AbsoluteEpisodeNumber is { } absolute)
			{
				releases.AddRange(await _torznabHttpClient.SearchAsync(indexer,
					$"{series.Title} {absolute:00}", AnimeCategories(indexer), cancellationToken));

				return releases.DistinctBy(release => release.Guid ?? release.DownloadUrl).ToList();
			}

			return releases;
		}, context);
	}

	private async Task<MediaContext> BuildSeriesContextAsync(Series series, int season,
		Submarine.Core.Quality.QualityModel? existingFileQuality,
		IReadOnlyList<Submarine.Core.Languages.Language>? existingFileLanguages, CancellationToken cancellationToken)
	{
		var qualityProfile = await GetQualityProfileAsync(series.QualityProfileId);

		var seasonFiles = await _seriesRepository.FindEpisodeFilesBySeasonAsync(series.Id, season);
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
			LanguageProfile = await GetLanguageProfileAsync(series.LanguageProfileId),
			Filters = await LoadFiltersAsync(),
			CustomFormats = await LoadFormatsAsync(),
			CustomFormatScores = qualityProfile.FormatScores,
			ExistingFileQuality = existingFileQuality,
			ExistingFileLanguages = existingFileLanguages,
			SeasonReleaseGroup = seasonReleaseGroup
		};
	}

	private async Task<IReadOnlyList<DownloadDecision>> SearchAsync(Protocol? protocol,
		CancellationToken cancellationToken, Func<Provider, Task<IReadOnlyList<ReleaseInfo>>> fetch, MediaContext context)
	{
		var indexers = await LoadIndexersAsync(protocol, cancellationToken);

		var candidates =
			(await Task.WhenAll(indexers.Select(indexer => FetchCandidatesAsync(indexer, fetch, cancellationToken))))
			.SelectMany(candidate => candidate)
			.ToList();

		return _decisionService.DecideAll(candidates, context);
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
					candidates.Add(new ReleaseCandidate(parsed, info, indexer.Name, indexer.Priority));
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
