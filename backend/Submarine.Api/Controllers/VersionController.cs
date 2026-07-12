using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.Library;

namespace Submarine.Api.Controllers;

[ApiController]
[Produces("application/json")]
public class VersionController : ControllerBase
{
	private readonly VersionService _service;

	public VersionController(VersionService service)
		=> _service = service;

	[HttpGet("api/v1/series/{seriesId:int}/versions")]
	[ProducesResponseType(typeof(IReadOnlyList<MediaVersionResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetForSeriesAsync([FromRoute] int seriesId)
		=> Ok(Map(await _service.GetForSeriesAsync(seriesId)));

	[HttpPost("api/v1/series/{seriesId:int}/versions")]
	[ProducesResponseType(typeof(MediaVersionResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateForSeriesAsync([FromRoute] int seriesId,
		[FromBody] AddMediaVersionRequest request)
	{
		var version = await _service.CreateForSeriesAsync(seriesId, request);

		return Created($"api/v1/version/{version.Id}", MediaVersionResponse.FromVersion(version));
	}

	[HttpGet("api/v1/movie/{movieId:int}/versions")]
	[ProducesResponseType(typeof(IReadOnlyList<MediaVersionResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetForMovieAsync([FromRoute] int movieId)
		=> Ok(Map(await _service.GetForMovieAsync(movieId)));

	[HttpPost("api/v1/movie/{movieId:int}/versions")]
	[ProducesResponseType(typeof(MediaVersionResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateForMovieAsync([FromRoute] int movieId,
		[FromBody] AddMediaVersionRequest request)
	{
		var version = await _service.CreateForMovieAsync(movieId, request);

		return Created($"api/v1/version/{version.Id}", MediaVersionResponse.FromVersion(version));
	}

	[HttpPut("api/v1/version/{id:int}")]
	[ProducesResponseType(typeof(MediaVersionResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateVersionRequest request)
	{
		var version = await _service.UpdateAsync(id, request);

		return Ok(MediaVersionResponse.FromVersion(version));
	}

	[HttpDelete("api/v1/version/{id:int}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool deleteFiles = false)
	{
		await _service.DeleteAsync(id, deleteFiles);

		return NoContent();
	}

	private static IReadOnlyList<MediaVersionResponse> Map(IReadOnlyList<MediaVersion> versions)
		=> versions.Select(MediaVersionResponse.FromVersion).ToList();
}
