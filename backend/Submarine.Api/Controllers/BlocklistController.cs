using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Download;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/blocklist")]
[Produces("application/json")]
public class BlocklistController : ControllerBase
{
	private readonly BlocklistService _service;

	public BlocklistController(BlocklistService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<BlocklistItem>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var items = await _service.GetPagedAsync(page, pageSize);

		return Ok(items);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(BlocklistItem), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}

	[HttpDelete("all")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteAllAsync()
	{
		await _service.DeleteAllAsync();

		return NoContent();
	}
}
