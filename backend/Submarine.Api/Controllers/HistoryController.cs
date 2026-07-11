using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Services;
using Submarine.Core.History;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HistoryController : ControllerBase
{
	private readonly HistoryService _service;

	public HistoryController(HistoryService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] HistoryEventType? type, [FromQuery] int? seriesId,
		[FromQuery] int? movieId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var history = await _service.GetPagedAsync(page, pageSize, type, seriesId, movieId);

		return Ok(history);
	}
}
