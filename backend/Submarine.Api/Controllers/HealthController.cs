using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
	private readonly HealthService _service;

	public HealthController(HealthService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<HealthIssue>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
	{
		var issues = await _service.CheckAsync(cancellationToken);

		return Ok(issues);
	}
}
