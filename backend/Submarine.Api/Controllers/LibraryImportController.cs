using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class LibraryImportController : ControllerBase
{
	private readonly LibraryImportService _service;

	public LibraryImportController(LibraryImportService service)
		=> _service = service;

	[HttpPost("scan")]
	[ProducesResponseType(typeof(IReadOnlyList<LibraryImportProposal>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ScanAsync([FromBody] LibraryImportScanRequest request,
		CancellationToken cancellationToken)
	{
		var proposals = await _service.ScanAsync(request, cancellationToken);

		return Ok(proposals);
	}

	[HttpPost]
	[ProducesResponseType(typeof(LibraryImportResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ImportAsync([FromBody] LibraryImportRequest request,
		CancellationToken cancellationToken)
	{
		var result = await _service.ImportAsync(request, cancellationToken);

		return Ok(result);
	}
}
