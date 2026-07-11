using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class QualityProfileController : ControllerBase
{
	private readonly ProfileService _service;

	public QualityProfileController(ProfileService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var profiles = await _service.GetAllQualityProfilesAsync(page, pageSize);

		return Ok(profiles);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var profile = await _service.GetQualityProfileAsync(id);

		return Ok(profile);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] CreateQualityProfileRequest request)
	{
		var profile = await _service.CreateQualityProfileAsync(request);

		return Created($"api/v1/qualityprofile/{profile.Id}", profile);
	}

	[HttpPut("{id:int}")]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateQualityProfileRequest request)
	{
		var profile = await _service.UpdateQualityProfileAsync(id, request);

		return Ok(profile);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteQualityProfileAsync(id);

		return Ok(deleted);
	}
}
