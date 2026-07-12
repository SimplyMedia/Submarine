using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Library;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class EpisodeController : ControllerBase
{
	private readonly EpisodeService _service;

	public EpisodeController(EpisodeService service)
		=> _service = service;

	[HttpPut("monitor")]
	[ProducesResponseType(typeof(List<Episode>), StatusCodes.Status200OK)]
	public async Task<IActionResult> BatchMonitorAsync([FromBody] BatchMonitorEpisodesRequest request)
	{
		var episodes = await _service.BatchMonitorAsync(request);

		return Ok(episodes);
	}
}
