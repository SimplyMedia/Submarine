using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ManualImportController : ControllerBase
{
	private readonly ManualImportService _service;

	public ManualImportController(ManualImportService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ManualImportCandidate>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ListAsync([FromQuery] string? folder = null,
		[FromQuery] int? trackedDownloadId = null, CancellationToken cancellationToken = default)
	{
		var candidates = await _service.ListAsync(folder, trackedDownloadId, cancellationToken);

		return Ok(candidates);
	}

	[HttpPost]
	[ProducesResponseType(typeof(ManualImportResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ImportAsync([FromBody] ManualImportRequest request,
		CancellationToken cancellationToken)
	{
		var result = await _service.ImportAsync(request, cancellationToken);

		return Ok(result);
	}
}
