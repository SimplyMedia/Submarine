using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class RootFolderController : ControllerBase
{
	private readonly RootFolderService _service;

	public RootFolderController(RootFolderService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var rootFolders = await _service.GetAllAsync(page, pageSize);

		return Ok(rootFolders);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var rootFolder = await _service.GetAsync(id);

		return Ok(RootFolderResponse.FromRootFolder(rootFolder));
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] CreateRootFolderRequest request)
	{
		var rootFolder = await _service.CreateAsync(request);

		return Created($"api/v1/rootfolder/{rootFolder.Id}", RootFolderResponse.FromRootFolder(rootFolder));
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(RootFolderResponse.FromRootFolder(deleted));
	}
}
