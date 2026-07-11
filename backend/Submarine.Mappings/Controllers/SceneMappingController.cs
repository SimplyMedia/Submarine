using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Mappings.Contracts;
using Submarine.Mappings.Database;
using Submarine.Mappings.Models;

namespace Submarine.Mappings.Controllers;

/// <summary>
///     Community-editable scene naming mappings
/// </summary>
[ApiController]
[Route("api/v1/scenemapping")]
public class SceneMappingController : ControllerBase
{
	private readonly MappingsDatabaseContext _context;

	/// <summary>
	///     Creates a new instance of <see cref="SceneMappingController" />
	/// </summary>
	/// <param name="context">database context to store mappings in</param>
	public SceneMappingController(MappingsDatabaseContext context)
		=> _context = context;

	/// <summary>
	///     Gets all scene mappings for a series, including per-episode overrides
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	[HttpGet("{tvdbId:int}")]
	public async Task<IActionResult> GetAsync([FromRoute] int tvdbId)
	{
		var mappings = await _context.SceneMappings.AsNoTracking()
			.Where(m => m.TvdbId == tvdbId)
			.Select(m => new SceneMappingResource(m.TvdbId, m.Title, m.SeasonNumber, m.SceneSeasonNumber,
				m.EpisodeOffset))
			.ToListAsync();

		var episodeMappings = await _context.SceneEpisodeMappings.AsNoTracking()
			.Where(m => m.TvdbId == tvdbId)
			.Select(m => new SceneEpisodeMappingResource(m.TvdbId, m.SeasonNumber, m.EpisodeNumber,
				m.SceneSeasonNumber, m.SceneEpisodeNumber))
			.ToListAsync();

		return Ok(new SceneMappingSetResource(mappings, episodeMappings));
	}

	/// <summary>
	///     Creates a new scene mapping
	/// </summary>
	/// <param name="request">mapping to create</param>
	[HttpPost]
	public async Task<IActionResult> CreateAsync([FromBody] SceneMappingResource request)
	{
		var entity = new SceneMapping
		{
			TvdbId = request.TvdbId,
			Title = request.Title,
			SeasonNumber = request.SeasonNumber,
			SceneSeasonNumber = request.SceneSeasonNumber,
			EpisodeOffset = request.EpisodeOffset
		};

		_context.SceneMappings.Add(entity);
		await _context.SaveChangesAsync();

		return Created($"api/v1/scenemapping/{entity.TvdbId}", request);
	}

	/// <summary>
	///     Updates an existing scene mapping
	/// </summary>
	/// <param name="id">identifier of the mapping to update</param>
	/// <param name="request">updated mapping data</param>
	[HttpPut("{id:int}")]
	public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] SceneMappingResource request)
	{
		var entity = await _context.SceneMappings.FirstOrDefaultAsync(m => m.Id == id);

		if (entity == null)
			return NotFound();

		entity.TvdbId = request.TvdbId;
		entity.Title = request.Title;
		entity.SeasonNumber = request.SeasonNumber;
		entity.SceneSeasonNumber = request.SceneSeasonNumber;
		entity.EpisodeOffset = request.EpisodeOffset;

		await _context.SaveChangesAsync();

		return Ok(request);
	}

	/// <summary>
	///     Deletes a scene mapping
	/// </summary>
	/// <param name="id">identifier of the mapping to delete</param>
	[HttpDelete("{id:int}")]
	public async Task<IActionResult> DeleteAsync([FromRoute] int id)
	{
		var entity = await _context.SceneMappings.FirstOrDefaultAsync(m => m.Id == id);

		if (entity == null)
			return NotFound();

		_context.SceneMappings.Remove(entity);
		await _context.SaveChangesAsync();

		return Ok();
	}
}
