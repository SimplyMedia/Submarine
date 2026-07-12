using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Download;
using Submarine.Core.Indexer.Torznab;
using Submarine.Core.Provider;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class IndexerController : ControllerBase
{
	private readonly IndexerService _service;

	public IndexerController(IndexerService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<Provider>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var indexers = await _service.GetPagedAsync(page, pageSize);

		return Ok(indexers);
	}

	[HttpPost("{id:int}/test")]
	[ProducesResponseType(typeof(TorznabCapabilities), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> TestAsync([FromRoute] int id, CancellationToken cancellationToken)
	{
		try
		{
			var capabilities = await _service.TestAsync(id, cancellationToken);

			return Ok(capabilities);
		}
		catch (DownloadClientException ex)
		{
			return Problem(ex.Message, statusCode: 400);
		}
	}
}
