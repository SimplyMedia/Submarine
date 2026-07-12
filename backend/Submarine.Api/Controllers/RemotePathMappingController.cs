using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Download;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class RemotePathMappingController : ControllerBase
{
	private readonly RemotePathMappingService _service;

	public RemotePathMappingController(RemotePathMappingService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<RemotePathMapping>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var mappings = await _service.GetAllAsync(page, pageSize);

		return Ok(mappings);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(RemotePathMapping), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var mapping = await _service.GetAsync(id);

		return Ok(mapping);
	}

	[HttpPost]
	[ProducesResponseType(typeof(RemotePathMapping), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateRemotePathMappingRequest request)
	{
		var mapping = await _service.CreateAsync(request);

		return Created($"api/v1/remotepathmapping/{mapping.Id}", mapping);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(RemotePathMapping), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateRemotePathMappingRequest request)
	{
		var mapping = await _service.UpdateAsync(id, request);

		return Ok(mapping);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(RemotePathMapping), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
