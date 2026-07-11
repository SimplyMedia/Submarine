using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Core.Download;
using Submarine.Core.Indexer;
using Submarine.Core.History;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Release.Exceptions;

namespace Submarine.Api.Services;

public class GrabService
{
	private readonly SubmarineDatabaseContext _context;
	private readonly DownloadClientFactory _factory;
	private readonly IParser<BaseRelease> _releaseParser;
	private readonly HistoryService _historyService;

	public GrabService(SubmarineDatabaseContext context, DownloadClientFactory factory,
		IParser<BaseRelease> releaseParser, HistoryService historyService)
	{
		_context = context;
		_factory = factory;
		_releaseParser = releaseParser;
		_historyService = historyService;
	}

	public async Task<TrackedDownload> GrabAsync(GrabReleaseRequest request, CancellationToken cancellationToken = default)
	{
		BaseRelease parsed;

		try
		{
			parsed = _releaseParser.Parse(request.Title);
		}
		catch (NotParsableReleaseException)
		{
			throw new BadRequestException($"release '{request.Title}' could not be parsed");
		}

		var (config, client) = await ResolveClientAsync(request.Protocol, cancellationToken);

		var releaseInfo = new ReleaseInfo
		{
			Title = request.Title,
			Guid = request.Guid,
			DownloadUrl = request.DownloadUrl,
			Size = request.Size,
			Protocol = request.Protocol
		};

		var downloadId = await client.AddDownloadAsync(releaseInfo, cancellationToken);

		var languages = parsed.Languages.ToList();

		var tracked = new TrackedDownload
		{
			DownloadClientConfigId = config.Id,
			DownloadId = downloadId,
			Title = request.Title,
			Protocol = request.Protocol,
			Status = DownloadItemStatus.QUEUED,
			ReleaseTitle = request.Title,
			Quality = parsed.Quality,
			Languages = languages,
			ReleaseGroup = parsed.ReleaseGroup,
			Indexer = request.Indexer,
			SeriesId = request.SeriesId,
			MovieId = request.MovieId,
			EpisodeIds = request.EpisodeIds?.ToList() ?? new List<int>()
		};

		await _context.TrackedDownloads.AddAsync(tracked, cancellationToken);
		await _context.SaveChangesAsync(cancellationToken);

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.GRABBED,
			SeriesId = request.SeriesId,
			EpisodeId = request.EpisodeIds is { Count: 1 } single ? single[0] : null,
			MovieId = request.MovieId,
			SourceTitle = request.Title,
			Quality = parsed.Quality,
			Languages = languages,
			Data = new Dictionary<string, string>
			{
				["downloadClient"] = config.Name,
				["downloadId"] = downloadId,
				["protocol"] = request.Protocol.ToString(),
				["indexer"] = request.Indexer ?? ""
			}
		}, cancellationToken);

		return tracked;
	}

	private async Task<(DownloadClientConfig Config, IDownloadClient Client)> ResolveClientAsync(Protocol protocol,
		CancellationToken cancellationToken)
	{
		var configs = await _context.DownloadClients.AsNoTracking()
			.Where(c => c.Enable)
			.OrderBy(c => c.Priority)
			.ThenBy(c => c.Id)
			.ToListAsync(cancellationToken);

		foreach (var config in configs)
		{
			var client = _factory.Create(config);

			if (client.Protocol == protocol)
				return (config, client);
		}

		throw new BadRequestException($"no enabled download client for protocol {protocol}");
	}
}
