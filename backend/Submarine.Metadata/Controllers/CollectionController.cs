using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Metadata.Clients;
using Submarine.Metadata.Contracts;

namespace Submarine.Metadata.Controllers;

/// <summary>
///     Endpoints for resolving normalized movie collection metadata
/// </summary>
[ApiController]
[Route("api/v1/collection")]
[Produces("application/json")]
public class CollectionController : ControllerBase
{
	private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(6);

	private readonly TmdbClient _client;
	private readonly IMemoryCache _cache;
	private readonly TimeSpan _cacheTtl;

	/// <summary>
	///     Creates a new instance of <see cref="CollectionController" />
	/// </summary>
	/// <param name="client">client to resolve collections from TMDB</param>
	/// <param name="cache">cache to store normalized results in</param>
	/// <param name="configuration">configuration to read the cache TTL from</param>
	public CollectionController(TmdbClient client, IMemoryCache cache, IConfiguration configuration)
	{
		_client = client;
		_cache = cache;
		_cacheTtl = configuration.GetValue<TimeSpan?>("Cache:Ttl") ?? DefaultCacheTtl;
	}

	/// <summary>
	///     Gets a single collection by its TMDB identifier
	/// </summary>
	/// <param name="tmdbCollectionId">TheMovieDB identifier of the collection</param>
	[HttpGet("{tmdbCollectionId:int}")]
	[ProducesResponseType(typeof(CollectionResource), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int tmdbCollectionId)
	{
		var collection = await _cache.GetOrCreateAsync(Request.GetEncodedPathAndQuery(), async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

			return await _client.GetCollectionAsync(tmdbCollectionId);
		});

		if (collection == null)
			return NotFound();

		return Ok(collection);
	}
}
