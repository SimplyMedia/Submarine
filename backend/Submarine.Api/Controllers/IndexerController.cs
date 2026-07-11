using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Services;
using Submarine.Core.Download;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class IndexerController : ControllerBase
{
	private readonly IndexerService _service;

	public IndexerController(IndexerService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var indexers = await _service.GetPagedAsync(page, pageSize);

		return Ok(indexers);
	}

	[HttpPost("{id:int}/test")]
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
