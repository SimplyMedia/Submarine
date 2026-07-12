using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Config;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
	private readonly SettingsService _service;

	public SettingsController(SettingsService service)
		=> _service = service;

	[HttpGet("naming")]
	[ProducesResponseType(typeof(NamingConfig), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetNamingAsync()
	{
		var config = await _service.GetNamingConfigAsync();

		return Ok(config);
	}

	[HttpPut("naming")]
	[ProducesResponseType(typeof(NamingConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateNamingAsync([FromBody] UpdateNamingConfigRequest request)
	{
		var config = await _service.UpdateNamingConfigAsync(request);

		return Ok(config);
	}

	[HttpGet("mediamanagement")]
	[ProducesResponseType(typeof(MediaManagementConfig), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMediaManagementAsync()
	{
		var config = await _service.GetMediaManagementConfigAsync();

		return Ok(config);
	}

	[HttpPut("mediamanagement")]
	[ProducesResponseType(typeof(MediaManagementConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateMediaManagementAsync([FromBody] UpdateMediaManagementConfigRequest request)
	{
		var config = await _service.UpdateMediaManagementConfigAsync(request);

		return Ok(config);
	}
}
