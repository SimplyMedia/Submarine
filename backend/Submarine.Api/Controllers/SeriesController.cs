using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class SeriesController : ControllerBase
{
	private readonly SeriesService _service;

	public SeriesController(SeriesService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<Series>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
		[FromQuery] bool? monitored = null, [FromQuery] SeriesType? type = null, [FromQuery] string? term = null)
	{
		var series = await _service.GetPagedAsync(page, pageSize, monitored, type, term);

		return Ok(series);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(SeriesResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var series = await _service.GetAsync(id);

		return Ok(series);
	}

	[HttpGet("lookup")]
	[ProducesResponseType(typeof(IReadOnlyList<SeriesResource>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> LookupAsync([FromQuery] string term)
	{
		var results = await _service.LookupAsync(term);

		return Ok(results);
	}

	[HttpGet("{id:int}/episodes")]
	[ProducesResponseType(typeof(PagedResult<Episode>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetEpisodesAsync([FromRoute] int id, [FromQuery] int page = 1,
		[FromQuery] int pageSize = 50, [FromQuery] int? season = null)
	{
		var episodes = await _service.GetEpisodesAsync(id, season, page, pageSize);

		return Ok(episodes);
	}

	[HttpPost]
	[ProducesResponseType(typeof(Series), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AddAsync([FromBody] AddSeriesRequest request)
	{
		var series = await _service.AddAsync(request);

		return Created($"api/v1/series/{series.Id}", series);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(Series), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateSeriesRequest request)
	{
		var series = await _service.UpdateAsync(id, request);

		return Ok(series);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(Series), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool deleteFiles = false)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
