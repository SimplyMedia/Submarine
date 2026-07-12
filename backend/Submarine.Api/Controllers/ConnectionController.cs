using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Notification;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ConnectionController : ControllerBase
{
	private readonly ConnectionService _service;

	public ConnectionController(ConnectionService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<Connection>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var connections = await _service.GetAllAsync(page, pageSize);

		return Ok(connections);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(Connection), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var connection = await _service.GetAsync(id);

		return Ok(connection);
	}

	[HttpPost]
	[ProducesResponseType(typeof(Connection), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateConnectionRequest request)
	{
		var connection = await _service.CreateAsync(request);

		return Created($"api/v1/connection/{connection.Id}", connection);
	}

	[HttpPatch("{id:int}")]
	[ProducesResponseType(typeof(Connection), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateConnectionRequest request)
	{
		var connection = await _service.UpdateAsync(id, request);

		return Ok(connection);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(Connection), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}

	[HttpPost("{id:int}/test")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> TestAsync([FromRoute] int id, CancellationToken cancellationToken)
	{
		try
		{
			await _service.TestAsync(id, cancellationToken);

			return NoContent();
		}
		catch (HttpRequestException ex)
		{
			return Problem(ex.Message, statusCode: 400);
		}
	}
}
