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

	[HttpGet("indexer")]
	[ProducesResponseType(typeof(IndexerConfig), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetIndexerAsync()
	{
		var config = await _service.GetIndexerConfigAsync();

		return Ok(config);
	}

	[HttpPut("indexer")]
	[ProducesResponseType(typeof(IndexerConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateIndexerAsync([FromBody] UpdateIndexerConfigRequest request)
	{
		var config = await _service.UpdateIndexerConfigAsync(request);

		return Ok(config);
	}

	[HttpGet("download")]
	[ProducesResponseType(typeof(DownloadConfig), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetDownloadAsync()
	{
		var config = await _service.GetDownloadConfigAsync();

		return Ok(config);
	}

	[HttpPut("download")]
	[ProducesResponseType(typeof(DownloadConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateDownloadAsync([FromBody] UpdateDownloadConfigRequest request)
	{
		var config = await _service.UpdateDownloadConfigAsync(request);

		return Ok(config);
	}

	[HttpGet("security")]
	[ProducesResponseType(typeof(SecurityConfig), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetSecurityAsync()
	{
		var config = await _service.GetSecurityConfigAsync();

		return Ok(config);
	}

	[HttpPut("security")]
	[ProducesResponseType(typeof(SecurityConfig), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateSecurityAsync([FromBody] UpdateSecurityConfigRequest request)
	{
		var config = await _service.UpdateSecurityConfigAsync(request);

		return Ok(config);
	}
}
