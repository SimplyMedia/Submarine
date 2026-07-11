using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Library;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SeriesController : ControllerBase
{
	private readonly SeriesService _service;

	public SeriesController(SeriesService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
		[FromQuery] bool? monitored = null, [FromQuery] SeriesType? type = null, [FromQuery] string? term = null)
	{
		var series = await _service.GetPagedAsync(page, pageSize, monitored, type, term);

		return Ok(series);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var series = await _service.GetAsync(id);

		return Ok(series);
	}

	[HttpGet("lookup")]
	public async Task<IActionResult> LookupAsync([FromQuery] string term)
	{
		var results = await _service.LookupAsync(term);

		return Ok(results);
	}

	[HttpGet("{id:int}/episodes")]
	public async Task<IActionResult> GetEpisodesAsync([FromRoute] int id, [FromQuery] int page = 1,
		[FromQuery] int pageSize = 50, [FromQuery] int? season = null)
	{
		var episodes = await _service.GetEpisodesAsync(id, season, page, pageSize);

		return Ok(episodes);
	}

	[HttpPost]
	public async Task<IActionResult> AddAsync([FromBody] AddSeriesRequest request)
	{
		var series = await _service.AddAsync(request);

		return Created($"api/v1/series/{series.Id}", series);
	}

	[HttpPut("{id:int}")]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateSeriesRequest request)
	{
		var series = await _service.UpdateAsync(id, request);

		return Ok(series);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool deleteFiles = false)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
