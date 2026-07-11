using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
public class SettingsController : ControllerBase
{
	private readonly SettingsService _service;

	public SettingsController(SettingsService service)
		=> _service = service;

	[HttpGet("naming")]
	public async Task<IActionResult> GetNamingAsync()
	{
		var config = await _service.GetNamingConfigAsync();

		return Ok(config);
	}

	[HttpPut("naming")]
	public async Task<IActionResult> UpdateNamingAsync([FromBody] UpdateNamingConfigRequest request)
	{
		var config = await _service.UpdateNamingConfigAsync(request);

		return Ok(config);
	}

	[HttpGet("mediamanagement")]
	public async Task<IActionResult> GetMediaManagementAsync()
	{
		var config = await _service.GetMediaManagementConfigAsync();

		return Ok(config);
	}

	[HttpPut("mediamanagement")]
	public async Task<IActionResult> UpdateMediaManagementAsync([FromBody] UpdateMediaManagementConfigRequest request)
	{
		var config = await _service.UpdateMediaManagementConfigAsync(request);

		return Ok(config);
	}
}
