using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Profile;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ReleaseProfileController : ControllerBase
{
	private readonly ReleaseProfileService _service;

	public ReleaseProfileController(ReleaseProfileService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<ReleaseProfile>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var profiles = await _service.GetAllAsync(page, pageSize);

		return Ok(profiles);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(ReleaseProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var profile = await _service.GetAsync(id);

		return Ok(profile);
	}

	[HttpPost]
	[ProducesResponseType(typeof(ReleaseProfile), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateReleaseProfileRequest request)
	{
		var profile = await _service.CreateAsync(request);

		return Created($"api/v1/releaseprofile/{profile.Id}", profile);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(ReleaseProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateReleaseProfileRequest request)
	{
		var profile = await _service.UpdateAsync(id, request);

		return Ok(profile);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(ReleaseProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
