using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using MetadataSeriesStatus = Submarine.Metadata.Contracts.SeriesStatus;

namespace Submarine.Api.Services;

public class SeriesService
{
	private readonly ISeriesRepository _repository;
	private readonly IRootFolderRepository _rootFolderRepository;
	private readonly IQualityProfileRepository _qualityProfileRepository;
	private readonly ILanguageProfileRepository _languageProfileRepository;
	private readonly IMetadataClient _metadataClient;
	private readonly IBackgroundTaskQueue _taskQueue;
	private readonly VersionService _versionService;

	public SeriesService(ISeriesRepository repository, IRootFolderRepository rootFolderRepository,
		IQualityProfileRepository qualityProfileRepository, ILanguageProfileRepository languageProfileRepository,
		IMetadataClient metadataClient, IBackgroundTaskQueue taskQueue, VersionService versionService)
	{
		_repository = repository;
		_rootFolderRepository = rootFolderRepository;
		_qualityProfileRepository = qualityProfileRepository;
		_languageProfileRepository = languageProfileRepository;
		_metadataClient = metadataClient;
		_taskQueue = taskQueue;
		_versionService = versionService;
	}

	public Task<PagedResult<Series>> GetPagedAsync(int page, int pageSize, bool? monitored, SeriesType? type,
		string? term)
	{
		var query = _repository.Query();

		if (monitored != null)
			query = query.Where(s => s.Monitored == monitored);

		if (type != null)
			query = query.Where(s => s.Type == type);

		if (!string.IsNullOrWhiteSpace(term))
			query = query.Where(s => s.Title.Contains(term));

		return query.OrderBy(s => s.SortTitle ?? s.Title).ToPagedResultAsync(page, pageSize);
	}

	public async Task<SeriesResponse> GetAsync(int id)
	{
		var series = await _repository.FindByIdWithSeasonsAsync(id);

		if (series == null)
			throw new NotFoundException();

		var episodeCount = await _repository.QueryEpisodes(id).CountAsync();

		return SeriesResponse.FromSeries(series, episodeCount);
	}

	public Task<IReadOnlyList<SeriesResource>> LookupAsync(string term,
		MetadataProvider provider = MetadataProvider.TVDB)
		=> _metadataClient.SearchSeriesAsync(term, provider);

	public async Task<Series> AddAsync(AddSeriesRequest request)
	{
		var existing = await _repository.FirstByConditionAsync(s => s.TvdbId == request.TvdbId);

		if (existing != null)
			throw new ConflictException($"Series with TVDB id '{request.TvdbId}' already exists");

		var resource = await _metadataClient.GetSeriesByTvdbAsync(request.TvdbId);

		if (resource == null)
			throw new BadRequestException("series not found");

		var numbering = request.Numbering ?? EpisodeNumbering.AIRED;
		var versions = await BuildVersionsAsync(request, resource.Title);

		await _versionService.EnsurePathsAvailableAsync(versions.Select(v => v.Path).ToList());

		var episodes = resource.Episodes.Select(e => MapEpisode(e, numbering, monitored: false)).ToList();
		ApplyMonitorOption(episodes, request.Monitor, request.Monitored);

		var monitoredSeasons = episodes.Where(e => e.Monitored).Select(e => e.SeasonNumber).ToHashSet();

		var series = new Series
		{
			TvdbId = resource.TvdbId,
			TmdbId = resource.TmdbId,
			Title = resource.Title,
			SortTitle = resource.SortTitle,
			Overview = resource.Overview,
			Network = resource.Network,
			Runtime = resource.Runtime,
			Year = resource.Year,
			Status = MapStatus(resource.Status),
			Type = request.Type ?? SeriesType.STANDARD,
			MetadataProvider = request.MetadataProvider ?? MetadataProvider.TVDB,
			Numbering = numbering,
			Monitored = request.Monitored,
			SeasonFolder = request.SeasonFolder,
			Tags = request.Tags,
			Versions = versions,
			Seasons = resource.Seasons
				.Select(s => new Season
					{ SeasonNumber = s.SeasonNumber, Monitored = monitoredSeasons.Contains(s.SeasonNumber) })
				.ToList(),
			Episodes = episodes
		};

		await _repository.CreateAsync(series);

		if (request.SearchOnAdd)
			foreach (var season in monitoredSeasons.OrderBy(s => s))
			{
				var target = season;

				await _taskQueue.QueueAsync((sp, ct) =>
					sp.GetRequiredService<AutomaticSearchService>().SearchAndGrabSeasonAsync(series.Id, target, ct));
			}

		return series;
	}

	// specials/season 0 are never auto-monitored; episode monitoring is additionally gated by the series monitored flag
	private static void ApplyMonitorOption(IReadOnlyList<Episode> episodes, MonitorOption option, bool seriesMonitored)
	{
		if (!seriesMonitored)
			return;

		var now = DateTimeOffset.UtcNow;
		var latestSeason = episodes.Where(e => e.SeasonNumber > 0).Select(e => e.SeasonNumber).DefaultIfEmpty(0).Max();

		foreach (var episode in episodes)
			episode.Monitored = episode.SeasonNumber != 0 && option switch
			{
				MonitorOption.ALL => true,
				MonitorOption.FUTURE => episode.AirDate == null || episode.AirDate > now,
				MonitorOption.MISSING => episode.AirDate != null && episode.AirDate <= now,
				MonitorOption.EXISTING => false,
				MonitorOption.PILOT => episode is { SeasonNumber: 1, EpisodeNumber: 1 },
				MonitorOption.FIRST_SEASON => episode.SeasonNumber == 1,
				MonitorOption.LATEST_SEASON => episode.SeasonNumber == latestSeason,
				MonitorOption.NONE => false,
				_ => true
			};
	}

	private async Task<List<MediaVersion>> BuildVersionsAsync(AddSeriesRequest request, string title)
	{
		var versions = new List<MediaVersion>
		{
			new()
			{
				Name = "Default",
				QualityProfileId = request.QualityProfileId,
				LanguageProfileId = request.LanguageProfileId,
				Path = await ResolvePathAsync(request.Path, request.RootFolderId, title),
				Monitored = request.Monitored
			}
		};

		foreach (var version in request.Versions)
			versions.Add(new MediaVersion
			{
				Name = version.Name,
				QualityProfileId = version.QualityProfileId,
				LanguageProfileId = version.LanguageProfileId,
				Path = await ResolvePathAsync(version.Path, version.RootFolderId, title),
				Monitored = version.Monitored
			});

		if (versions.Select(v => v.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != versions.Count)
			throw new BadRequestException("versions must resolve to distinct paths");

		return versions;
	}

	public async Task<Series> UpdateAsync(int id, UpdateSeriesRequest request)
	{
		var series = await _repository.FirstByConditionAsync(s => s.Id == id);

		if (series == null)
			throw new NotFoundException();

		if (request.Monitored != null)
			series.Monitored = request.Monitored.Value;
		if (request.Type != null)
			series.Type = request.Type.Value;
		if (request.Tags != null)
			series.Tags = request.Tags;

		var refreshNeeded = false;

		if (request.MetadataProvider != null && request.MetadataProvider.Value != series.MetadataProvider)
		{
			series.MetadataProvider = request.MetadataProvider.Value;
			refreshNeeded = true;
		}

		if (request.Numbering != null && request.Numbering.Value != series.Numbering)
		{
			series.Numbering = request.Numbering.Value;
			refreshNeeded = true;
		}

		await _repository.UpdateAsync(series);

		if (refreshNeeded)
			await QueueRefreshAsync(series.Id);

		return series;
	}

	private ValueTask QueueRefreshAsync(int seriesId)
		=> _taskQueue.QueueAsync(async (sp, ct) =>
		{
			var context = sp.GetRequiredService<SubmarineDatabaseContext>();
			var series = await context.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct);

			if (series != null)
				await sp.GetRequiredService<SeriesRefreshService>().RefreshSeriesAsync(series, ct);
		});

	public async Task<Series> DeleteAsync(int id, bool deleteFiles)
	{
		var series = await _repository.FirstByConditionAsync(s => s.Id == id);

		if (series == null)
			throw new NotFoundException();

		if (deleteFiles)
		{
			var versions = (await _repository.FindVersionsAsync(id)).ToDictionary(v => v.Id, v => v.Path);

			foreach (var file in await _repository.FindEpisodeFilesAsync(id))
				if (versions.TryGetValue(file.MediaVersionId, out var versionPath))
					DeleteFromDisk(Path.Combine(versionPath, file.RelativePath));
		}

		await _repository.DeleteAsync(series);

		return series;
	}

	public async Task<int> EditorAsync(SeriesEditorRequest request)
	{
		if (request.QualityProfileId != null
		    && await _qualityProfileRepository.FirstByConditionAsync(p => p.Id == request.QualityProfileId) == null)
			throw new BadRequestException($"Quality profile with id '{request.QualityProfileId}' does not exist");

		if (request.LanguageProfileId != null
		    && await _languageProfileRepository.FirstByConditionAsync(p => p.Id == request.LanguageProfileId) == null)
			throw new BadRequestException($"Language profile with id '{request.LanguageProfileId}' does not exist");

		var series = await _repository.FindByIdsWithVersionsAsync(request.SeriesIds);

		foreach (var item in series)
		{
			if (request.Monitored != null)
				item.Monitored = request.Monitored.Value;

			if (request.QualityProfileId != null || request.LanguageProfileId != null)
			{
				var defaultVersion = item.Versions.OrderBy(v => v.Id).First();

				if (request.QualityProfileId != null)
					defaultVersion.QualityProfileId = request.QualityProfileId.Value;
				if (request.LanguageProfileId != null)
					defaultVersion.LanguageProfileId = request.LanguageProfileId.Value;
			}

			if (request.AddTags != null)
				item.Tags = item.Tags.Union(request.AddTags).ToList();
			if (request.RemoveTags != null)
				item.Tags = item.Tags.Except(request.RemoveTags).ToList();
		}

		await _repository.UpdateAsync(series);

		return series.Count;
	}

	public async Task<Season> SetSeasonMonitoredAsync(int seriesId, int seasonNumber, bool monitored)
	{
		var season = await _repository.FindSeasonAsync(seriesId, seasonNumber);

		if (season == null)
			throw new NotFoundException();

		season.Monitored = monitored;

		var episodes = await _repository.FindEpisodesForSeasonAsync(seriesId, seasonNumber);

		foreach (var episode in episodes)
			episode.Monitored = monitored;

		await _repository.SaveSeasonWithEpisodesAsync(season, episodes);

		return season;
	}

	public async Task<PagedResult<Episode>> GetEpisodesAsync(int seriesId, int? season, int page, int pageSize)
	{
		var series = await _repository.FirstByConditionAsync(s => s.Id == seriesId);

		if (series == null)
			throw new NotFoundException();

		var query = _repository.QueryEpisodes(seriesId);

		if (season != null)
			query = query.Where(e => e.SeasonNumber == season);

		return await query
			.OrderBy(e => e.SeasonNumber)
			.ThenBy(e => e.EpisodeNumber)
			.ToPagedResultAsync(page, pageSize);
	}

	private async Task<string> ResolvePathAsync(string? requestPath, int? rootFolderId, string title)
	{
		if (rootFolderId != null)
		{
			var rootFolder = await _rootFolderRepository.FirstByConditionAsync(r => r.Id == rootFolderId);

			if (rootFolder == null)
				throw new BadRequestException($"Root folder with id '{rootFolderId}' does not exist");

			return Path.Combine(rootFolder.Path, SanitizeFolderName(title));
		}

		if (!string.IsNullOrWhiteSpace(requestPath))
			return requestPath;

		throw new BadRequestException("either Path or RootFolderId must be provided");
	}

	private static Episode MapEpisode(EpisodeResource resource, EpisodeNumbering numbering, bool monitored)
	{
		var absolute = resource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Absolute);
		var (seasonNumber, episodeNumber) = EpisodeNumberResolver.Resolve(resource, numbering);

		return new Episode
		{
			SeasonNumber = seasonNumber,
			EpisodeNumber = episodeNumber,
			AbsoluteEpisodeNumber = absolute?.AbsoluteNumber,
			TvdbId = resource.TvdbId,
			TmdbId = resource.TmdbId,
			Title = resource.Title,
			Overview = resource.Overview,
			AirDate = resource.AirDate == null
				? null
				: new DateTimeOffset(resource.AirDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
			Runtime = resource.Runtime,
			Monitored = monitored
		};
	}

	private static Core.Library.SeriesStatus MapStatus(MetadataSeriesStatus status)
		=> status switch
		{
			MetadataSeriesStatus.Continuing => Core.Library.SeriesStatus.CONTINUING,
			MetadataSeriesStatus.Ended => Core.Library.SeriesStatus.ENDED,
			MetadataSeriesStatus.Upcoming => Core.Library.SeriesStatus.UPCOMING,
			_ => Core.Library.SeriesStatus.UNKNOWN
		};

	private static string SanitizeFolderName(string name)
		=> string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

	private static void DeleteFromDisk(string path)
	{
		if (File.Exists(path))
			File.Delete(path);
	}
}
