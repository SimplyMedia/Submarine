using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ProviderController : ControllerBase
{
	private readonly ProviderService _service;

	public ProviderController(ProviderService service)
		=> _service = service;

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var provider = await _service.GetAsync(id);

		return Ok(provider);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] CreateProviderRequest request)
	{
		var provider = await _service.CreateAsync(request);

		return Created($"provider/{provider.Id}", provider);
	}

	[HttpPatch("{id:int}")]
	public async Task<IActionResult> ModifyAsync([FromRoute] int id, [FromBody] UpdateProviderRequest request)
	{
		var updated = await _service.UpdateAsync(id, request);

		return Ok(updated);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
