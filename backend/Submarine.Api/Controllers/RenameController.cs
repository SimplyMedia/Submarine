using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class RenameController : ControllerBase
{
	private readonly RenameService _service;

	public RenameController(RenameService service)
		=> _service = service;

	[HttpGet("preview")]
	[ProducesResponseType(typeof(IReadOnlyList<RenamePreviewItem>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> PreviewAsync([FromQuery] int seriesId)
	{
		var preview = await _service.PreviewSeriesAsync(seriesId);

		return Ok(preview);
	}

	[HttpPost]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> RenameAsync([FromQuery] int seriesId)
	{
		var queued = await _service.RenameSeriesAsync(seriesId);

		return Accepted(new { queued });
	}
}
