using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Mappings.Contracts;
using Submarine.Mappings.Database;
using Submarine.Mappings.Models;
using Submarine.Mappings.Services;

namespace Submarine.Mappings.Controllers;

/// <summary>
///     Community-editable TVDB to AniList arc/season mappings
/// </summary>
[ApiController]
[Produces("application/json")]
public class AniListMappingController : ControllerBase
{
	private readonly MappingsDatabaseContext _context;
	private readonly AniListMappingService _service;

	/// <summary>
	///     Creates a new instance of <see cref="AniListMappingController" />
	/// </summary>
	/// <param name="context">database context to store mappings in</param>
	/// <param name="service">service to resolve mappings with</param>
	public AniListMappingController(MappingsDatabaseContext context, AniListMappingService service)
	{
		_context = context;
		_service = service;
	}

	/// <summary>
	///     Gets the mapping for an AniList entry
	/// </summary>
	/// <param name="aniListId">AniList identifier of the entry</param>
	[HttpGet("api/v1/anilist/{aniListId:int}")]
	[ProducesResponseType(typeof(AniListMappingResource), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync([FromRoute] int aniListId)
	{
		var mapping = await _context.AniListMappings.AsNoTracking()
			.FirstOrDefaultAsync(m => m.AniListId == aniListId);

		if (mapping == null)
			return Problem($"No AniList mapping exists for entry {aniListId}", statusCode: StatusCodes.Status404NotFound);

		return Ok(ToResource(mapping));
	}

	/// <summary>
	///     Resolves the TVDB numbering for an AniList episode
	/// </summary>
	/// <param name="aniListId">AniList identifier of the entry</param>
	/// <param name="episode">episode number within the AniList entry, 1-based</param>
	[HttpGet("api/v1/anilist/{aniListId:int}/resolve")]
	[ProducesResponseType(typeof(TvdbResolution), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ResolveTvdbAsync([FromRoute] int aniListId, [FromQuery] int episode)
	{
		var resolution = await _service.ResolveTvdbAsync(aniListId, episode);

		if (resolution == null)
			return Problem($"No TVDB mapping exists for AniList entry {aniListId} episode {episode}",
				statusCode: StatusCodes.Status404NotFound);

		return Ok(resolution);
	}

	/// <summary>
	///     Gets all AniList mappings for a series
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	[HttpGet("api/v1/tvdb/{tvdbId:int}/anilist")]
	[ProducesResponseType(typeof(IEnumerable<AniListMappingResource>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByTvdbAsync([FromRoute] int tvdbId)
	{
		var mappings = await _service.GetByTvdbAsync(tvdbId);

		return Ok(mappings.Select(ToResource));
	}

	/// <summary>
	///     Resolves the AniList numbering for a TVDB season episode
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="season">TVDB season number</param>
	/// <param name="episode">TVDB season-relative episode number, 1-based</param>
	[HttpGet("api/v1/tvdb/{tvdbId:int}/anilist/resolve")]
	[ProducesResponseType(typeof(AniListResolution), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ResolveAniListAsync([FromRoute] int tvdbId, [FromQuery] int season,
		[FromQuery] int episode)
	{
		var resolution = await _service.ResolveAniListAsync(tvdbId, season, episode);

		if (resolution == null)
			return Problem($"No AniList mapping exists for TVDB series {tvdbId} season {season} episode {episode}",
				statusCode: StatusCodes.Status404NotFound);

		return Ok(resolution);
	}

	/// <summary>
	///     Creates a new AniList mapping
	/// </summary>
	/// <param name="request">mapping to create</param>
	[HttpPost("api/v1/anilist")]
	[ProducesResponseType(typeof(AniListMappingResource), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateAsync([FromBody] AniListMappingResource request)
	{
		var entity = new AniListMapping
		{
			AniListId = request.AniListId,
			TvdbId = request.TvdbId,
			Title = request.Title,
			TvdbSeason = request.TvdbSeason,
			EpisodeStart = request.EpisodeStart,
			EpisodeCount = request.EpisodeCount,
			AbsoluteOffset = request.AbsoluteOffset
		};

		_context.AniListMappings.Add(entity);
		await _context.SaveChangesAsync();

		return Created($"api/v1/anilist/{entity.AniListId}", request);
	}

	/// <summary>
	///     Updates an existing AniList mapping
	/// </summary>
	/// <param name="id">identifier of the mapping to update</param>
	/// <param name="request">updated mapping data</param>
	[HttpPut("api/v1/anilist/{id:int}")]
	[ProducesResponseType(typeof(AniListMappingResource), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] AniListMappingResource request)
	{
		var entity = await _context.AniListMappings.FirstOrDefaultAsync(m => m.Id == id);

		if (entity == null)
			return NotFound();

		entity.AniListId = request.AniListId;
		entity.TvdbId = request.TvdbId;
		entity.Title = request.Title;
		entity.TvdbSeason = request.TvdbSeason;
		entity.EpisodeStart = request.EpisodeStart;
		entity.EpisodeCount = request.EpisodeCount;
		entity.AbsoluteOffset = request.AbsoluteOffset;

		await _context.SaveChangesAsync();

		return Ok(request);
	}

	/// <summary>
	///     Deletes an AniList mapping
	/// </summary>
	/// <param name="id">identifier of the mapping to delete</param>
	[HttpDelete("api/v1/anilist/{id:int}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var entity = await _context.AniListMappings.FirstOrDefaultAsync(m => m.Id == id);

		if (entity == null)
			return NotFound();

		_context.AniListMappings.Remove(entity);
		await _context.SaveChangesAsync();

		return Ok();
	}

	private static AniListMappingResource ToResource(AniListMapping m)
		=> new(m.AniListId, m.TvdbId, m.Title, m.TvdbSeason, m.EpisodeStart, m.EpisodeCount, m.AbsoluteOffset);
}
