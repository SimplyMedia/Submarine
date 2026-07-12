using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
	private readonly StatsService _service;

	public StatsController(StatsService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(LibraryStats), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
	{
		var stats = await _service.GetStatsAsync(cancellationToken);

		return Ok(stats);
	}
}
