using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class QueueController : ControllerBase
{
	private readonly QueueService _service;

	public QueueController(QueueService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var queue = await _service.GetPagedAsync(page, pageSize);

		return Ok(queue);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool removeFromClient = true,
		[FromQuery] bool deleteData = false, CancellationToken cancellationToken = default)
	{
		await _service.DeleteAsync(id, removeFromClient, deleteData, cancellationToken);

		return NoContent();
	}

	[HttpPost("{id:int}/import")]
	public async Task<IActionResult> ImportAsync([FromRoute] int id, CancellationToken cancellationToken)
	{
		await _service.ImportAsync(id, cancellationToken);

		return Accepted();
	}
}
