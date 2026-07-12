using Microsoft.EntityFrameworkCore;
using Submarine.Api.Clients;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;

namespace Submarine.Api.Services;

public class CollectionService
{
	private readonly IMovieRepository _movieRepository;
	private readonly IMetadataClient _metadataClient;
	private readonly MovieService _movieService;

	public CollectionService(IMovieRepository movieRepository, IMetadataClient metadataClient,
		MovieService movieService)
	{
		_movieRepository = movieRepository;
		_metadataClient = metadataClient;
		_movieService = movieService;
	}

	public async Task<IReadOnlyList<CollectionListItem>> GetAllAsync()
	{
		var movies = await _movieRepository.Query()
			.Include(m => m.Files)
			.Where(m => m.TmdbCollectionId != null)
			.ToListAsync();

		return movies
			.GroupBy(m => m.TmdbCollectionId!.Value)
			.Select(g => new CollectionListItem(
				g.Key,
				g.First().CollectionTitle ?? g.First().Title,
				g.Count(),
				g.Select(m => new CollectionListMovie(m.Id, m.Title, m.Year, m.Monitored, m.Files.Count > 0)).ToList()))
			.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public async Task<CollectionDetailResponse> GetAsync(int tmdbCollectionId, CancellationToken cancellationToken = default)
	{
		var libraryMovies = await _movieRepository.Query()
			.Include(m => m.Files)
			.Where(m => m.TmdbCollectionId == tmdbCollectionId)
			.ToListAsync(cancellationToken);

		var resource = await _metadataClient.GetCollectionAsync(tmdbCollectionId, cancellationToken);

		var title = resource?.Title ?? libraryMovies.FirstOrDefault()?.CollectionTitle;

		if (title == null)
			throw new NotFoundException();

		var libraryTmdbIds = libraryMovies.Select(m => m.TmdbId).ToHashSet();

		var entries = libraryMovies
			.Select(m => new CollectionEntry(true, m.Id, m.TmdbId, m.Title, m.Year, m.Monitored, m.Files.Count > 0))
			.ToList();

		if (resource != null)
			entries.AddRange(resource.Movies
				.Where(mr => !libraryTmdbIds.Contains(mr.TmdbId))
				.Select(mr => new CollectionEntry(false, null, mr.TmdbId, mr.Title, mr.Year, null, null)));

		return new CollectionDetailResponse(tmdbCollectionId, title, resource?.Overview, entries);
	}

	public async Task<CollectionAddResult> AddMissingAsync(int tmdbCollectionId, AddCollectionMoviesRequest request,
		CancellationToken cancellationToken = default)
	{
		var resource = await _metadataClient.GetCollectionAsync(tmdbCollectionId, cancellationToken);

		if (resource == null)
			throw new NotFoundException();

		var libraryTmdbIds = (await _movieRepository.FindByConditionAsync(m => m.TmdbCollectionId == tmdbCollectionId))
			.Select(m => m.TmdbId).ToHashSet();

		var added = new List<CollectionAddedMovie>();
		var failed = new List<CollectionAddFailure>();

		foreach (var movie in resource.Movies.Where(m => !libraryTmdbIds.Contains(m.TmdbId)))
			try
			{
				var created = await _movieService.AddAsync(new AddMovieRequest
				{
					TmdbId = movie.TmdbId,
					RootFolderId = request.RootFolderId,
					QualityProfileId = request.QualityProfileId,
					LanguageProfileId = request.LanguageProfileId,
					Monitored = request.Monitored,
					SearchOnAdd = request.SearchOnAdd
				});

				added.Add(new CollectionAddedMovie(created.Id, created.TmdbId, created.Title));
			}
			catch (Exception ex)
			{
				failed.Add(new CollectionAddFailure(movie.TmdbId, movie.Title, ex.Message));
			}

		return new CollectionAddResult(added, failed);
	}
}
