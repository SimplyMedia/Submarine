using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TagController : ControllerBase
{
	private readonly TagService _service;

	public TagController(TagService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var tags = await _service.GetAllAsync(page, pageSize);

		return Ok(tags);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var tag = await _service.GetAsync(id);

		return Ok(tag);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] CreateTagRequest request)
	{
		var tag = await _service.CreateAsync(request);

		return Created($"api/v1/tag/{tag.Id}", tag);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
