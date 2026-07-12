using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Quality;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/qualityoverride")]
[Produces("application/json")]
public class QualityOverrideController : ControllerBase
{
	private readonly QualityOverrideService _service;

	public QualityOverrideController(QualityOverrideService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(List<ReleaseGroupQualityOverride>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync()
	{
		var overrides = await _service.GetAllAsync();

		return Ok(overrides);
	}

	[HttpPost]
	[ProducesResponseType(typeof(ReleaseGroupQualityOverride), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateQualityOverrideRequest request)
	{
		var created = await _service.CreateAsync(request);

		return Created($"api/v1/qualityoverride/{created.Id}", created);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(ReleaseGroupQualityOverride), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateQualityOverrideRequest request)
	{
		var updated = await _service.UpdateAsync(id, request);

		return Ok(updated);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(ReleaseGroupQualityOverride), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
