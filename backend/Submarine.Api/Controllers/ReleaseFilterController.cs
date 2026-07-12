using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.DecisionEngine.Filter;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ReleaseFilterController : ControllerBase
{
	private readonly DecisionConfigService _service;

	public ReleaseFilterController(DecisionConfigService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<ReleaseFilterConfig>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var filters = await _service.GetAllFiltersAsync(page, pageSize);

		return Ok(filters);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(ReleaseFilterConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var filter = await _service.GetFilterAsync(id);

		return Ok(filter);
	}

	[HttpPost]
	[ProducesResponseType(typeof(ReleaseFilterConfig), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateReleaseFilterRequest request)
	{
		var filter = await _service.CreateFilterAsync(request);

		return Created($"api/v1/releasefilter/{filter.Id}", filter);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(ReleaseFilterConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateReleaseFilterRequest request)
	{
		var filter = await _service.UpdateFilterAsync(id, request);

		return Ok(filter);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(ReleaseFilterConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteFilterAsync(id);

		return Ok(deleted);
	}
}
