using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ReleaseController : ControllerBase
{
	private readonly ILogger<ReleaseController> _logger;
	private readonly IParser<TorrentRelease> _torrentReleaseParserService;
	private readonly IParser<UsenetRelease> _usenetReleaseParserService;

	public ReleaseController(ILogger<ReleaseController> logger,
		IParser<TorrentRelease> torrentReleaseParserService, IParser<UsenetRelease> usenetReleaseParserService)
	{
		_logger = logger;
		_torrentReleaseParserService = torrentReleaseParserService;
		_usenetReleaseParserService = usenetReleaseParserService;
	}

	[HttpGet]
	public Task<IActionResult> GetAsync([FromQuery] [Required] string title, [FromQuery] [Required] Protocol protocol)
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

			return Task.FromResult(Ok(parsed) as IActionResult);
		}
		catch (NotParsableReleaseException)
		{
			return Task.FromResult(UnprocessableEntity() as IActionResult);
		}
	}
}
