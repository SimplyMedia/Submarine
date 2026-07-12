using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Metadata.Clients;
using Submarine.Metadata.Contracts;

namespace Submarine.Metadata.Controllers;

/// <summary>
///     Endpoints for resolving normalized movie metadata
/// </summary>
[ApiController]
[Route("api/v1/movie")]
[Produces("application/json")]
public class MovieController : ControllerBase
{
	private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(6);

	private readonly TmdbClient _client;
	private readonly IMemoryCache _cache;
	private readonly TimeSpan _cacheTtl;

	/// <summary>
	///     Creates a new instance of <see cref="MovieController" />
	/// </summary>
	/// <param name="client">client to resolve movies from TMDB</param>
	/// <param name="cache">cache to store normalized results in</param>
	/// <param name="configuration">configuration to read the cache TTL from</param>
	public MovieController(TmdbClient client, IMemoryCache cache, IConfiguration configuration)
	{
		_client = client;
		_cache = cache;
		_cacheTtl = configuration.GetValue<TimeSpan?>("Cache:Ttl") ?? DefaultCacheTtl;
	}

	/// <summary>
	///     Gets a single movie by its TMDB identifier
	/// </summary>
	/// <param name="tmdbId">TheMovieDB identifier of the movie</param>
	[HttpGet("{tmdbId:int}")]
	[ProducesResponseType(typeof(MovieResource), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int tmdbId)
	{
		var movie = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return await _client.GetMovieAsync(tmdbId);
		});

		if (movie == null)
			return NotFound();

		return Ok(movie);
	}

	/// <summary>
	///     Searches for movies matching the given term
	/// </summary>
	/// <param name="term">search term</param>
	[HttpGet("search")]
	[ProducesResponseType(typeof(IReadOnlyList<MovieResource>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> SearchAsync([FromQuery] string term)
	{
		var results = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return await _client.SearchMoviesAsync(term);
		});

		return Ok(results);
	}
}
