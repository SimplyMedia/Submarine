using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LanguageProfileController : ControllerBase
{
	private readonly ProfileService _service;

	public LanguageProfileController(ProfileService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var profiles = await _service.GetAllLanguageProfilesAsync(page, pageSize);

		return Ok(profiles);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var profile = await _service.GetLanguageProfileAsync(id);

		return Ok(profile);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] CreateLanguageProfileRequest request)
	{
		var profile = await _service.CreateLanguageProfileAsync(request);

		return Created($"api/v1/languageprofile/{profile.Id}", profile);
	}

	[HttpPut("{id:int}")]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateLanguageProfileRequest request)
	{
		var profile = await _service.UpdateLanguageProfileAsync(id, request);

		return Ok(profile);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteLanguageProfileAsync(id);

		return Ok(deleted);
	}
}
