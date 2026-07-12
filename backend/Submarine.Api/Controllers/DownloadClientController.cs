using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Download;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class DownloadClientController : ControllerBase
{
	private readonly DownloadClientService _service;

	public DownloadClientController(DownloadClientService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<DownloadClientConfig>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var clients = await _service.GetAllAsync(page, pageSize);

		return Ok(clients);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(DownloadClientConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var client = await _service.GetAsync(id);

		return Ok(client);
	}

	[HttpPost]
	[ProducesResponseType(typeof(DownloadClientConfig), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateDownloadClientRequest request)
	{
		var client = await _service.CreateAsync(request);

		return Created($"api/v1/downloadclient/{client.Id}", client);
	}

	[HttpPatch("{id:int}")]
	[ProducesResponseType(typeof(DownloadClientConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateDownloadClientRequest request)
	{
		var client = await _service.UpdateAsync(id, request);

		return Ok(client);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(DownloadClientConfig), StatusCodes.Status200OK)]
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
		catch (DownloadClientException ex)
		{
			return Problem(ex.Message, statusCode: 400);
		}
	}
}
