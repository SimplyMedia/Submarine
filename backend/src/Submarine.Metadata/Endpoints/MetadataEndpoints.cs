using Microsoft.AspNetCore.RateLimiting;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Upstream;

namespace Submarine.Metadata.Endpoints;

public static class MetadataEndpoints
{
	public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder app)
	{
		var api = app.MapGroup("/api/v1").RequireRateLimiting("api");

		api.MapGet("/series/tvdb/{tvdbId:int}",
			(int tvdbId, MetadataService service, CancellationToken cancellationToken)
				=> Detail(service.GetSeriesByTvdbAsync(tvdbId, cancellationToken)));
		api.MapGet("/series/tmdb/{tmdbId:int}",
			(int tmdbId, MetadataService service, CancellationToken cancellationToken)
				=> Detail(service.GetSeriesByTmdbAsync(tmdbId, cancellationToken)));
		api.MapGet("/series/search",
			(string term, string? provider, MetadataService service, CancellationToken cancellationToken)
				=> List(service.SearchSeriesAsync(term, provider, cancellationToken)));
		api.MapGet("/series/popular",
			(int page, MetadataService service, CancellationToken cancellationToken)
				=> List(service.GetPopularSeriesAsync(page, cancellationToken)));

		api.MapGet("/movie/{tmdbId:int}",
			(int tmdbId, MetadataService service, CancellationToken cancellationToken)
				=> Detail(service.GetMovieAsync(tmdbId, cancellationToken)));
		api.MapGet("/movie/imdb/{imdbId}",
			(string imdbId, MetadataService service, CancellationToken cancellationToken)
				=> Detail(service.GetMovieByImdbAsync(imdbId, cancellationToken)));
		api.MapGet("/movie/search",
			(string term, int? year, MetadataService service, CancellationToken cancellationToken)
				=> List(service.SearchMoviesAsync(term, year, cancellationToken)));
		api.MapGet("/movie/popular",
			(int page, MetadataService service, CancellationToken cancellationToken)
				=> List(service.GetPopularMoviesAsync(page, cancellationToken)));
		api.MapGet("/movie/list/{listId:int}",
			(int listId, MetadataService service, CancellationToken cancellationToken)
				=> List(service.GetMovieListAsync(listId, cancellationToken)));
		api.MapGet("/movie/person/{personId:int}",
			(int personId, MetadataService service, CancellationToken cancellationToken)
				=> List(service.GetMoviesByPersonAsync(personId, cancellationToken)));

		api.MapGet("/collection/{tmdbCollectionId:int}",
			(int tmdbCollectionId, MetadataService service, CancellationToken cancellationToken)
				=> Detail(service.GetCollectionAsync(tmdbCollectionId, cancellationToken)));

		return app;
	}

	private static async Task<IResult> Detail<T>(ValueTask<T?> lookup) where T : class
	{
		T? resource;
		try
		{
			resource = await lookup;
		}
		catch (UpstreamException exception)
		{
			return Problem(exception);
		}

		return resource is null
			? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found")
			: Results.Json(resource);
	}

	private static async Task<IResult> List<T>(ValueTask<IReadOnlyList<T>> lookup)
	{
		IReadOnlyList<T> resources;
		try
		{
			resources = await lookup;
		}
		catch (UpstreamException exception)
		{
			return Problem(exception);
		}

		return Results.Json(resources);
	}

	private static IResult Problem(UpstreamException exception) =>
		Results.Problem(
			statusCode: StatusCodes.Status502BadGateway,
			title: "Upstream request failed",
			detail: exception.Message,
			extensions: new Dictionary<string, object?> { ["upstreamStatus"] = exception.StatusCode });
}
