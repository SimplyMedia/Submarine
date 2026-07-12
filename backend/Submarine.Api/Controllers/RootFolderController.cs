using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class RootFolderController : ControllerBase
{
	private readonly RootFolderService _service;

	public RootFolderController(RootFolderService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<RootFolderResponse>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var rootFolders = await _service.GetAllAsync(page, pageSize);

		return Ok(rootFolders);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(RootFolderResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var rootFolder = await _service.GetAsync(id);

		return Ok(RootFolderResponse.FromRootFolder(rootFolder));
	}

	[HttpPost]
	[ProducesResponseType(typeof(RootFolderResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateRootFolderRequest request)
	{
		var rootFolder = await _service.CreateAsync(request);

		return Created($"api/v1/rootfolder/{rootFolder.Id}", RootFolderResponse.FromRootFolder(rootFolder));
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(RootFolderResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(RootFolderResponse.FromRootFolder(deleted));
	}
}
