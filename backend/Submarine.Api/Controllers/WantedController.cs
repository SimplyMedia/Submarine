using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/wanted")]
[Produces("application/json")]
public class WantedController : ControllerBase
{
	private readonly WantedService _service;

	public WantedController(WantedService service)
		=> _service = service;

	[HttpGet("missing")]
	[ProducesResponseType(typeof(PagedResult<WantedMissingItem>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMissingAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
		CancellationToken cancellationToken = default)
		=> Ok(await _service.GetMissingAsync(page, pageSize, cancellationToken));

	[HttpGet("cutoff")]
	[ProducesResponseType(typeof(PagedResult<WantedCutoffItem>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetCutoffAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
		CancellationToken cancellationToken = default)
		=> Ok(await _service.GetCutoffAsync(page, pageSize, cancellationToken));

	[HttpPost("missing/search")]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	public async Task<IActionResult> SearchMissingAsync()
	{
		await _service.EnqueueMissingSearchAsync();

		return Accepted();
	}

	[HttpPost("cutoff/search")]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	public async Task<IActionResult> SearchCutoffAsync()
	{
		await _service.EnqueueCutoffSearchAsync();

		return Accepted();
	}
}
