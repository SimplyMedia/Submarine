using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
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
	private readonly IMetadataClient _metadataClient;

	public SeriesService(ISeriesRepository repository, IRootFolderRepository rootFolderRepository,
		IMetadataClient metadataClient)
	{
		_repository = repository;
		_rootFolderRepository = rootFolderRepository;
		_metadataClient = metadataClient;
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

	public Task<IReadOnlyList<SeriesResource>> LookupAsync(string term)
		=> _metadataClient.SearchSeriesAsync(term);

	public async Task<Series> AddAsync(AddSeriesRequest request)
	{
		var existing = await _repository.FirstByConditionAsync(s => s.TvdbId == request.TvdbId);

		if (existing != null)
			throw new ConflictException($"Series with TVDB id '{request.TvdbId}' already exists");

		var resource = await _metadataClient.GetSeriesAsync(request.TvdbId);

		if (resource == null)
			throw new BadRequestException("series not found");

		var versions = await BuildVersionsAsync(request, resource.Title);

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
			Monitored = request.Monitored,
			SeasonFolder = request.SeasonFolder,
			Tags = request.Tags,
			Versions = versions,
			Seasons = resource.Seasons
				.Select(s => new Season { SeasonNumber = s.SeasonNumber, Monitored = request.Monitored })
				.ToList(),
			Episodes = resource.Episodes.Select(e => MapEpisode(e, request.Monitored)).ToList()
		};

		await _repository.CreateAsync(series);

		return series;
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

		await _repository.UpdateAsync(series);

		return series;
	}

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

	private static Episode MapEpisode(EpisodeResource resource, bool monitored)
	{
		var aired = resource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Aired);
		var absolute = resource.Numbers.FirstOrDefault(n => n.Ordering == EpisodeOrdering.Absolute);

		return new Episode
		{
			SeasonNumber = aired?.SeasonNumber ?? 0,
			EpisodeNumber = aired?.Number ?? 0,
			AbsoluteEpisodeNumber = absolute?.AbsoluteNumber,
			TvdbId = resource.TvdbId,
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
