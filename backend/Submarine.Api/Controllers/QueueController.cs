using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class QueueController : ControllerBase
{
	private readonly QueueService _service;

	public QueueController(QueueService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<QueueItemResponse>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var queue = await _service.GetPagedAsync(page, pageSize);

		return Ok(queue);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool removeFromClient = true,
		[FromQuery] bool deleteData = false, CancellationToken cancellationToken = default)
	{
		await _service.DeleteAsync(id, removeFromClient, deleteData, cancellationToken);

		return NoContent();
	}

	[HttpPost("{id:int}/import")]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ImportAsync([FromRoute] int id, CancellationToken cancellationToken)
	{
		await _service.ImportAsync(id, cancellationToken);

		return Accepted();
	}
}
