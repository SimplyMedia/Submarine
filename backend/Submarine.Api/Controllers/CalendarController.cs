using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Response;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CalendarController : ControllerBase
{
	private static readonly TimeSpan FeedLookback = TimeSpan.FromDays(7);
	private static readonly TimeSpan FeedLookahead = TimeSpan.FromDays(30);

	private readonly CalendarService _service;
	private readonly SecurityConfigStore _securityConfigStore;

	public CalendarController(CalendarService service, SecurityConfigStore securityConfigStore)
	{
		_service = service;
		_securityConfigStore = securityConfigStore;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CalendarItemResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetAsync([FromQuery] DateTimeOffset? start = null,
		[FromQuery] DateTimeOffset? end = null)
	{
		var rangeStart = start ?? new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
		var rangeEnd = end ?? rangeStart.AddDays(7);

		var items = await _service.GetAsync(rangeStart, rangeEnd);

		return Ok(items);
	}

	/// <summary>
	///     iCal feed of upcoming episode air dates and movie releases, authenticated by a token query parameter
	///     since calendar apps can't send an Authorization header. This path is exempted from
	///     <see cref="Middleware.ApiKeyMiddleware" /> and enforces the token itself.
	/// </summary>
	[HttpGet("feed")]
	[ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> GetFeedAsync([FromQuery] string? token)
	{
		var config = await _securityConfigStore.GetAsync();

		if (!IsValidToken(token, config.FeedToken))
			return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
				detail: "A valid feed token is required via the 'token' query parameter");

		var now = DateTimeOffset.UtcNow;
		var items = await _service.GetAsync(now - FeedLookback, now + FeedLookahead);

		return Content(IcsWriter.Write(items), "text/calendar");
	}

	// constant-time compare avoids leaking the token through response timing; a null/empty stored token never
	// authorizes
	private static bool IsValidToken(string? provided, string? stored)
	{
		if (provided == null || string.IsNullOrEmpty(stored))
			return false;

		return CryptographicOperations.FixedTimeEquals(
			Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(stored));
	}
}
