using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class RenameController : ControllerBase
{
	private readonly RenameService _service;

	public RenameController(RenameService service)
		=> _service = service;

	[HttpGet("preview")]
	public async Task<IActionResult> PreviewAsync([FromQuery] int seriesId)
	{
		var preview = await _service.PreviewSeriesAsync(seriesId);

		return Ok(preview);
	}

	[HttpPost]
	public async Task<IActionResult> RenameAsync([FromQuery] int seriesId)
	{
		var queued = await _service.RenameSeriesAsync(seriesId);

		return Accepted(new { queued });
	}
}
