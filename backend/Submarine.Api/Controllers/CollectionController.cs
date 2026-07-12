using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CollectionController : ControllerBase
{
	private readonly CollectionService _service;

	public CollectionController(CollectionService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CollectionListItem>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync()
	{
		var collections = await _service.GetAllAsync();

		return Ok(collections);
	}

	[HttpGet("{tmdbCollectionId:int}")]
	[ProducesResponseType(typeof(CollectionDetailResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int tmdbCollectionId, CancellationToken cancellationToken)
	{
		var collection = await _service.GetAsync(tmdbCollectionId, cancellationToken);

		return Ok(collection);
	}

	[HttpPost("{tmdbCollectionId:int}/add")]
	[ProducesResponseType(typeof(CollectionAddResult), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AddAsync([FromRoute] int tmdbCollectionId,
		[FromBody] AddCollectionMoviesRequest request, CancellationToken cancellationToken)
	{
		var result = await _service.AddMissingAsync(tmdbCollectionId, request, cancellationToken);

		return Ok(result);
	}
}
