using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Provider;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
	private readonly SearchService _service;

	public SearchController(SearchService service)
		=> _service = service;

	[HttpGet]
	[ProducesResponseType(typeof(List<SearchDecisionResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> SearchAsync(
		[FromQuery] int? seriesId,
		[FromQuery] int? seasonNumber,
		[FromQuery] int? episodeId,
		[FromQuery] int? episodeNumber,
		[FromQuery] int? movieId,
		[FromQuery] string? term,
		[FromQuery] Protocol? protocol,
		CancellationToken cancellationToken)
	{
		var decisions = await ResolveAsync(seriesId, seasonNumber, episodeId, episodeNumber, movieId, term, protocol,
			cancellationToken);

		return Ok(decisions.Select(SearchDecisionResponse.FromDecision).ToList());
	}

	private Task<IReadOnlyList<DownloadDecision>> ResolveAsync(int? seriesId, int? seasonNumber, int? episodeId,
		int? episodeNumber, int? movieId, string? term, Protocol? protocol, CancellationToken cancellationToken)
	{
		if (movieId != null)
			return _service.SearchMovieAsync(movieId.Value, cancellationToken);

		if (!string.IsNullOrWhiteSpace(term))
			return _service.SearchTermAsync(term, protocol, cancellationToken);

		if (episodeId != null)
			return _service.SearchEpisodeByIdAsync(episodeId.Value, cancellationToken);

		if (seriesId != null && seasonNumber != null && episodeNumber != null)
			return _service.SearchEpisodeAsync(seriesId.Value, seasonNumber.Value, episodeNumber.Value,
				cancellationToken);

		if (seriesId != null && seasonNumber != null)
			return _service.SearchSeasonAsync(seriesId.Value, seasonNumber.Value, cancellationToken);

		throw new BadRequestException(
			"provide movieId, term, episodeId, seriesId with seasonNumber and episodeNumber, or seriesId with seasonNumber");
	}
}
