using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CustomFormatController : ControllerBase
{
	private readonly DecisionConfigService _service;

	public CustomFormatController(DecisionConfigService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var formats = await _service.GetAllFormatsAsync(page, pageSize);

		return Ok(formats);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var format = await _service.GetFormatAsync(id);

		return Ok(format);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] CreateCustomFormatRequest request)
	{
		var format = await _service.CreateFormatAsync(request);

		return Created($"api/v1/customformat/{format.Id}", format);
	}

	[HttpPut("{id:int}")]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateCustomFormatRequest request)
	{
		var format = await _service.UpdateFormatAsync(id, request);

		return Ok(format);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteFormatAsync(id);

		return Ok(deleted);
	}
}
