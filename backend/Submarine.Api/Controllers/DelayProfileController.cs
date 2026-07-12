using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Profile;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class DelayProfileController : ControllerBase
{
	private readonly DelayProfileService _service;

	public DelayProfileController(DelayProfileService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(List<DelayProfile>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync()
	{
		var profiles = await _service.GetAllAsync();

		return Ok(profiles);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(DelayProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var profile = await _service.GetAsync(id);

		return Ok(profile);
	}

	[HttpPost]
	[ProducesResponseType(typeof(DelayProfile), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateDelayProfileRequest request)
	{
		var profile = await _service.CreateAsync(request);

		return Created($"api/v1/delayprofile/{profile.Id}", profile);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(DelayProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateDelayProfileRequest request)
	{
		var profile = await _service.UpdateAsync(id, request);

		return Ok(profile);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(DelayProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
