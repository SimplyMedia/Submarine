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
public class MovieController : ControllerBase
{
	private readonly MovieService _service;

	public MovieController(MovieService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(PagedResult<Movie>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
		[FromQuery] bool? monitored = null, [FromQuery] string? term = null, [FromQuery] bool? isAnime = null)
	{
		var movies = await _service.GetPagedAsync(page, pageSize, monitored, isAnime, term);

		return Ok(movies);
	}

	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(MovieResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int id)
	{
		var movie = await _service.GetAsync(id);

		return Ok(movie);
	}

	[HttpGet("lookup")]
	[ProducesResponseType(typeof(IReadOnlyList<MovieResource>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> LookupAsync([FromQuery] string term)
	{
		var results = await _service.LookupAsync(term);

		return Ok(results);
	}

	[HttpPost]
	[ProducesResponseType(typeof(Movie), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AddAsync([FromBody] AddMovieRequest request)
	{
		var movie = await _service.AddAsync(request);

		return Created($"api/v1/movie/{movie.Id}", movie);
	}

	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(Movie), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateMovieRequest request)
	{
		var movie = await _service.UpdateAsync(id, request);

		return Ok(movie);
	}

	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(Movie), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool deleteFiles = false)
	{
		var deleted = await _service.DeleteAsync(id, deleteFiles);

		return Ok(deleted);
	}

	[HttpPost("editor")]
	[ProducesResponseType(typeof(EditorResult), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> EditorAsync([FromBody] MovieEditorRequest request)
	{
		var updated = await _service.EditorAsync(request);

		return Ok(new EditorResult(updated));
	}
}
