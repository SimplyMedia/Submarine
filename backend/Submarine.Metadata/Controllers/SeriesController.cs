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

	private readonly TvdbClient _client;
	private readonly IMemoryCache _cache;
	private readonly TimeSpan _cacheTtl;

	/// <summary>
	///     Creates a new instance of <see cref="SeriesController" />
	/// </summary>
	/// <param name="client">client to resolve series from TVDB</param>
	/// <param name="cache">cache to store normalized results in</param>
	/// <param name="configuration">configuration to read the cache TTL from</param>
	public SeriesController(TvdbClient client, IMemoryCache cache, IConfiguration configuration)
	{
		_client = client;
		_cache = cache;
		_cacheTtl = configuration.GetValue<TimeSpan?>("Cache:Ttl") ?? DefaultCacheTtl;
	}

	/// <summary>
	///     Gets a single series by its TVDB identifier, including every episode ordering
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	[HttpGet("{tvdbId:int}")]
	[ProducesResponseType(typeof(SeriesResource), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int tvdbId)
	{
		var series = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return await _client.GetSeriesAsync(tvdbId);
		});

		if (series == null)
			return NotFound();

		return Ok(series);
	}

	/// <summary>
	///     Searches for series matching the given term
	/// </summary>
	/// <param name="term">search term</param>
	[HttpGet("search")]
	[ProducesResponseType(typeof(IReadOnlyList<SeriesResource>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> SearchAsync([FromQuery] string term)
	{
		var results = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return await _client.SearchSeriesAsync(term);
		});

		return Ok(results);
	}
}
