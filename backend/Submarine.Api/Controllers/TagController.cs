using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class TagController : ControllerBase
{
	private readonly TagService _service;

	public TagController(TagService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<Core.Tag.Tag>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var tags = await _service.GetAllAsync(page, pageSize);

		return Ok(tags);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(Core.Tag.Tag), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var tag = await _service.GetAsync(id);

		return Ok(tag);
	}

	[HttpPost]
	[ProducesResponseType(typeof(Core.Tag.Tag), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateTagRequest request)
	{
		var tag = await _service.CreateAsync(request);

		return Created($"api/v1/tag/{tag.Id}", tag);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(Core.Tag.Tag), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
