using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Provider;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ProviderController : ControllerBase
{
	private readonly ProviderService _service;

	public ProviderController(ProviderService service)
		=> _service = service;

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(Provider), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var provider = await _service.GetAsync(id);

		return Ok(provider);
	}

	[HttpPost]
	[ProducesResponseType(typeof(Provider), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateProviderRequest request)
	{
		var provider = await _service.CreateAsync(request);

		return Created($"api/v1/provider/{provider.Id}", provider);
	}

	[HttpPatch("{id:int}")]
	[ProducesResponseType(typeof(Provider), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ModifyAsync([FromRoute] int id, [FromBody] UpdateProviderRequest request)
	{
		var updated = await _service.UpdateAsync(id, request);

		return Ok(updated);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(Provider), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
