using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ImportListController : ControllerBase
{
	private readonly ImportListService _service;

	public ImportListController(ImportListService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var lists = await _service.GetAllAsync(page, pageSize);

		return Ok(lists);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var list = await _service.GetAsync(id);

		return Ok(list);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] CreateImportListRequest request)
	{
		var list = await _service.CreateAsync(request);

		return Created($"api/v1/importlist/{list.Id}", list);
	}

	[HttpPatch("{id:int}")]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateImportListRequest request)
	{
		var list = await _service.UpdateAsync(id, request);

		return Ok(list);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
