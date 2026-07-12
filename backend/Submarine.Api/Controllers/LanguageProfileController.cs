using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Profile;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class LanguageProfileController : ControllerBase
{
	private readonly ProfileService _service;

	public LanguageProfileController(ProfileService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<LanguageProfile>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var profiles = await _service.GetAllLanguageProfilesAsync(page, pageSize);

		return Ok(profiles);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(LanguageProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var profile = await _service.GetLanguageProfileAsync(id);

		return Ok(profile);
	}

	[HttpPost]
	[ProducesResponseType(typeof(LanguageProfile), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateLanguageProfileRequest request)
	{
		var profile = await _service.CreateLanguageProfileAsync(request);

		return Created($"api/v1/languageprofile/{profile.Id}", profile);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(LanguageProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateLanguageProfileRequest request)
	{
		var profile = await _service.UpdateLanguageProfileAsync(id, request);

		return Ok(profile);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(LanguageProfile), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteLanguageProfileAsync(id);

		return Ok(deleted);
	}
}
