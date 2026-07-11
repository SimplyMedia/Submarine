using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MovieController : ControllerBase
{
	private readonly MovieService _service;

	public MovieController(MovieService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
		[FromQuery] bool? monitored = null, [FromQuery] string? term = null, [FromQuery] bool? isAnime = null)
	{
		var movies = await _service.GetPagedAsync(page, pageSize, monitored, isAnime, term);

		return Ok(movies);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var movie = await _service.GetAsync(id);

		return Ok(movie);
	}

	[HttpGet("lookup")]
	public async Task<IActionResult> LookupAsync([FromQuery] string term)
	{
		var results = await _service.LookupAsync(term);

		return Ok(results);
	}

	[HttpPost]
	public async Task<IActionResult> AddAsync([FromBody] AddMovieRequest request)
	{
		var movie = await _service.AddAsync(request);

		return Created($"api/v1/movie/{movie.Id}", movie);
	}

	[HttpPut("{id:int}")]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateMovieRequest request)
	{
		var movie = await _service.UpdateAsync(id, request);

		return Ok(movie);
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool deleteFiles = false)
	{
		var deleted = await _service.DeleteAsync(id);

		return Ok(deleted);
	}
}
