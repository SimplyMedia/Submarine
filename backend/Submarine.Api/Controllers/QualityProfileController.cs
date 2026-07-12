using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Profile;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class QualityProfileController : ControllerBase
{
	private readonly ProfileService _service;

	public QualityProfileController(ProfileService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<QualityProfile>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var profiles = await _service.GetAllQualityProfilesAsync(page, pageSize);

		return Ok(profiles);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(QualityProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var profile = await _service.GetQualityProfileAsync(id);

		return Ok(profile);
	}

	[HttpPost]
	[ProducesResponseType(typeof(QualityProfile), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateQualityProfileRequest request)
	{
		var profile = await _service.CreateQualityProfileAsync(request);

		return Created($"api/v1/qualityprofile/{profile.Id}", profile);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(QualityProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateQualityProfileRequest request)
	{
		var profile = await _service.UpdateQualityProfileAsync(id, request);

		return Ok(profile);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(QualityProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteQualityProfileAsync(id);

		return Ok(deleted);
	}
}
