using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;

namespace Submarine.Api.Services;

public class MovieService
{
	private readonly IMovieRepository _repository;
	private readonly IRootFolderRepository _rootFolderRepository;
	private readonly IMetadataClient _metadataClient;
	private readonly IBackgroundTaskQueue _taskQueue;

	public MovieService(IMovieRepository repository, IRootFolderRepository rootFolderRepository,
		IMetadataClient metadataClient, IBackgroundTaskQueue taskQueue)
	{
		_repository = repository;
		_rootFolderRepository = rootFolderRepository;
		_metadataClient = metadataClient;
		_taskQueue = taskQueue;
	}

	public Task<PagedResult<Movie>> GetPagedAsync(int page, int pageSize, bool? monitored, bool? isAnime,
		string? term)
	{
		var query = _repository.Query();

		if (monitored != null)
			query = query.Where(m => m.Monitored == monitored);

		if (isAnime != null)
			query = query.Where(m => m.IsAnime == isAnime);

		if (!string.IsNullOrWhiteSpace(term))
			query = query.Where(m => m.Title.Contains(term));

		return query.OrderBy(m => m.SortTitle ?? m.Title).ToPagedResultAsync(page, pageSize);
	}

	public async Task<MovieResponse> GetAsync(int id)
	{
		var movie = await _repository.FindByIdWithVersionsAsync(id);

		if (movie == null)
			throw new NotFoundException();

		return MovieResponse.FromMovie(movie);
	}

	public Task<IReadOnlyList<MovieResource>> LookupAsync(string term)
		=> _metadataClient.SearchMoviesAsync(term);

	public async Task<Movie> AddAsync(AddMovieRequest request)
	{
		var existing = await _repository.FirstByConditionAsync(m => m.TmdbId == request.TmdbId);

		if (existing != null)
			throw new ConflictException($"Movie with TMDB id '{request.TmdbId}' already exists");

		var resource = await _metadataClient.GetMovieAsync(request.TmdbId);

		if (resource == null)
			throw new BadRequestException("movie not found");

		var versions = await BuildVersionsAsync(request, resource.Title, resource.Year);

		var movie = new Movie
		{
			TmdbId = resource.TmdbId,
			ImdbId = resource.ImdbId,
			Title = resource.Title,
			SortTitle = resource.SortTitle,
			Overview = resource.Overview,
			Year = resource.Year,
			Runtime = resource.Runtime,
			Studio = resource.Studio,
			ReleaseDate = resource.ReleaseDate == null
				? null
				: new DateTimeOffset(resource.ReleaseDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
			IsAnime = request.IsAnime ?? false,
			Monitored = request.Monitored,
			Tags = request.Tags,
			Versions = versions
		};

		await _repository.CreateAsync(movie);

		if (request.SearchOnAdd && movie.Monitored)
			await _taskQueue.QueueAsync((sp, ct) =>
				sp.GetRequiredService<AutomaticSearchService>().SearchAndGrabMovieAsync(movie.Id, ct));

		return movie;
	}

	private async Task<List<MediaVersion>> BuildVersionsAsync(AddMovieRequest request, string title, int? year)
	{
		var versions = new List<MediaVersion>
		{
			new()
			{
				Name = "Default",
				QualityProfileId = request.QualityProfileId,
				LanguageProfileId = request.LanguageProfileId,
				Path = await ResolvePathAsync(request.Path, request.RootFolderId, title, year),
				Monitored = request.Monitored
			}
		};

		foreach (var version in request.Versions)
			versions.Add(new MediaVersion
			{
				Name = version.Name,
				QualityProfileId = version.QualityProfileId,
				LanguageProfileId = version.LanguageProfileId,
				Path = await ResolvePathAsync(version.Path, version.RootFolderId, title, year),
				Monitored = version.Monitored
			});

		if (versions.Select(v => v.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != versions.Count)
			throw new BadRequestException("versions must resolve to distinct paths");

		return versions;
	}

	public async Task<Movie> UpdateAsync(int id, UpdateMovieRequest request)
	{
		var movie = await _repository.FirstByConditionAsync(m => m.Id == id);

		if (movie == null)
			throw new NotFoundException();

		if (request.Monitored != null)
			movie.Monitored = request.Monitored.Value;
		if (request.IsAnime != null)
			movie.IsAnime = request.IsAnime.Value;
		if (request.Tags != null)
			movie.Tags = request.Tags;

		await _repository.UpdateAsync(movie);

		return movie;
	}

	public async Task<Movie> DeleteAsync(int id, bool deleteFiles)
	{
		var movie = await _repository.FirstByConditionAsync(m => m.Id == id);

		if (movie == null)
			throw new NotFoundException();

		if (deleteFiles)
		{
			var versions = (await _repository.FindVersionsAsync(id)).ToDictionary(v => v.Id, v => v.Path);

			foreach (var file in await _repository.FindMovieFilesAsync(id))
				if (versions.TryGetValue(file.MediaVersionId, out var versionPath))
					DeleteFromDisk(Path.Combine(versionPath, file.RelativePath));
		}

		await _repository.DeleteAsync(movie);

		return movie;
	}

	private async Task<string> ResolvePathAsync(string? requestPath, int? rootFolderId, string title, int? year)
	{
		if (rootFolderId != null)
		{
			var rootFolder = await _rootFolderRepository.FirstByConditionAsync(r => r.Id == rootFolderId);

			if (rootFolder == null)
				throw new BadRequestException($"Root folder with id '{rootFolderId}' does not exist");

			var folderName = year != null ? $"{title} ({year})" : title;

			return Path.Combine(rootFolder.Path, SanitizeFolderName(folderName));
		}

		if (!string.IsNullOrWhiteSpace(requestPath))
			return requestPath;

		throw new BadRequestException("either Path or RootFolderId must be provided");
	}

	private static string SanitizeFolderName(string name)
		=> string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

	private static void DeleteFromDisk(string path)
	{
		if (File.Exists(path))
			File.Delete(path);
	}
}
