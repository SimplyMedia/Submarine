using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Services;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CalendarController : ControllerBase
{
	private readonly CalendarService _service;

	public CalendarController(CalendarService service)
		=> _service = service;

	[HttpGet]
	public async Task<IActionResult> GetAsync([FromQuery] DateTimeOffset? start = null,
		[FromQuery] DateTimeOffset? end = null)
	{
		var rangeStart = start ?? new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
		var rangeEnd = end ?? rangeStart.AddDays(7);

		var items = await _service.GetAsync(rangeStart, rangeEnd);

		return Ok(items);
	}
}
