using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.DecisionEngine.CustomFormats;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CustomFormatController : ControllerBase
{
	private readonly DecisionConfigService _service;

	public CustomFormatController(DecisionConfigService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<CustomFormatConfig>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var formats = await _service.GetAllFormatsAsync(page, pageSize);

		return Ok(formats);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(CustomFormatConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var format = await _service.GetFormatAsync(id);

		return Ok(format);
	}

	[HttpPost]
	[ProducesResponseType(typeof(CustomFormatConfig), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateCustomFormatRequest request)
	{
		var format = await _service.CreateFormatAsync(request);

		return Created($"api/v1/customformat/{format.Id}", format);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(CustomFormatConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateCustomFormatRequest request)
	{
		var format = await _service.UpdateFormatAsync(id, request);

		return Ok(format);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(CustomFormatConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteFormatAsync(id);

		return Ok(deleted);
	}
}
