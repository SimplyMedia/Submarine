using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

/// <summary>
///     Manages database and configuration backups. There is no restore endpoint: to restore a backup, stop
///     Submarine, replace the database file with the one contained in the downloaded zip, then start Submarine
///     again.
/// </summary>
[ApiController]
[Route("api/v1/backup")]
[Produces("application/json")]
public class BackupController : ControllerBase
{
	private readonly BackupService _service;

	public BackupController(BackupService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<BackupEntryResponse>), StatusCodes.Status200OK)]
	public IActionResult List()
		=> Ok(_service.List());

	[HttpPost]
	[ProducesResponseType(typeof(BackupEntryResponse), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateAsync(CancellationToken cancellationToken)
	{
		var entry = await _service.CreateAsync(cancellationToken);

		return Created($"api/v1/backup/{entry.Name}/download", entry);
	}

	[HttpDelete("{name}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public IActionResult Delete([FromRoute] string name)
	{
		_service.Delete(name);

		return NoContent();
	}

	[HttpGet("{name}/download")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public IActionResult Download([FromRoute] string name)
	{
		var path = _service.ResolvePath(name);
		var stream = System.IO.File.OpenRead(path);

		return File(stream, "application/zip", name);
	}
}
