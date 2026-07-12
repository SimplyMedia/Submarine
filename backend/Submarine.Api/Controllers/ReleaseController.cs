using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Download;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ReleaseController : ControllerBase
{
	private readonly ILogger<ReleaseController> _logger;
	private readonly IParser<TorrentRelease> _torrentReleaseParserService;
	private readonly IParser<UsenetRelease> _usenetReleaseParserService;
	private readonly GrabService _grabService;

	public ReleaseController(ILogger<ReleaseController> logger,
		IParser<TorrentRelease> torrentReleaseParserService, IParser<UsenetRelease> usenetReleaseParserService,
		GrabService grabService)
	{
		_logger = logger;
		_torrentReleaseParserService = torrentReleaseParserService;
		_usenetReleaseParserService = usenetReleaseParserService;
		_grabService = grabService;
	}

	[HttpGet]
	[ProducesResponseType(typeof(BaseRelease), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
	public IActionResult Get([FromQuery] [Required] string title, [FromQuery] [Required] Protocol protocol)
	{
		try
		{
			BaseRelease parsed = protocol switch
			{
				Protocol.BITTORRENT => _torrentReleaseParserService.Parse(title),
				Protocol.USENET => _usenetReleaseParserService.Parse(title),
				Protocol.XDCC => throw new NotImplementedException(),
				_ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
			};

			return Ok(parsed);
		}
		catch (NotParsableReleaseException)
		{
			return UnprocessableEntity();
		}
	}

	[HttpPost("grab")]
	[ProducesResponseType(typeof(TrackedDownload), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GrabAsync([FromBody] GrabReleaseRequest request,
		CancellationToken cancellationToken)
	{
		var tracked = await _grabService.GrabAsync(request, cancellationToken);

		return Ok(tracked);
	}
}
