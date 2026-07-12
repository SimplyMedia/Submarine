using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Metadata.Clients;
using Submarine.Metadata.Contracts;

namespace Submarine.Metadata.Controllers;

/// <summary>
///     Endpoints for resolving normalized series metadata
/// </summary>
[ApiController]
[Route("api/v1/series")]
[Produces("application/json")]
public class SeriesController : ControllerBase
{
	private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(6);

	private readonly TvdbClient _tvdbClient;
	private readonly TmdbClient _tmdbClient;
	private readonly IMemoryCache _cache;
	private readonly TimeSpan _cacheTtl;

	/// <summary>
	///     Creates a new instance of <see cref="SeriesController" />
	/// </summary>
	/// <param name="tvdbClient">client to resolve series from TVDB</param>
	/// <param name="tmdbClient">client to resolve series from TMDB</param>
	/// <param name="cache">cache to store normalized results in</param>
	/// <param name="configuration">configuration to read the cache TTL from</param>
	public SeriesController(TvdbClient tvdbClient, TmdbClient tmdbClient, IMemoryCache cache,
		IConfiguration configuration)
	{
		_tvdbClient = tvdbClient;
		_tmdbClient = tmdbClient;
		_cache = cache;
		_cacheTtl = configuration.GetValue<TimeSpan?>("Cache:Ttl") ?? DefaultCacheTtl;
	}

	/// <summary>
	///     Gets a single series by its TVDB identifier, including every episode ordering
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	[HttpGet("{tvdbId:int}")]
	[HttpGet("tvdb/{tvdbId:int}")]
	[ProducesResponseType(typeof(SeriesResource), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int tvdbId)
	{
		var series = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return await _tvdbClient.GetSeriesAsync(tvdbId);
		});

		if (series == null)
			return NotFound();

		return Ok(series);
	}

	/// <summary>
	///     Gets a single series by its TMDB identifier, with aired-only episode ordering
	/// </summary>
	/// <param name="tmdbId">TheMovieDB identifier of the series</param>
	[HttpGet("tmdb/{tmdbId:int}")]
	[ProducesResponseType(typeof(SeriesResource), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetFromTmdbAsync([FromRoute] int tmdbId)
	{
		var series = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return await _tmdbClient.GetSeriesAsync(tmdbId);
		});

		if (series == null)
			return NotFound();

		return Ok(series);
	}

	/// <summary>
	///     Searches for series matching the given term against the requested provider
	/// </summary>
	/// <param name="term">search term</param>
	/// <param name="provider">metadata provider to search, either tvdb or tmdb (defaults to tvdb)</param>
	[HttpGet("search")]
	[ProducesResponseType(typeof(IReadOnlyList<SeriesResource>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> SearchAsync([FromQuery] string term, [FromQuery] string provider = "tvdb")
	{
		var results = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return string.Equals(provider, "tmdb", StringComparison.OrdinalIgnoreCase)
				? await _tmdbClient.SearchSeriesAsync(term)
				: await _tvdbClient.SearchSeriesAsync(term);
		});

		return Ok(results);
	}
}
