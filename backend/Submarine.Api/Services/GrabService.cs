using Microsoft.EntityFrameworkCore;
using Submarine.Api.Events;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
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
	private readonly IEventPublisher _eventPublisher;
	private readonly IProviderRepository _providerRepository;

	public GrabService(SubmarineDatabaseContext context, DownloadClientFactory factory,
		IParser<BaseRelease> releaseParser, HistoryService historyService, IEventPublisher eventPublisher,
		IProviderRepository providerRepository)
	{
		_context = context;
		_factory = factory;
		_releaseParser = releaseParser;
		_historyService = historyService;
		_eventPublisher = eventPublisher;
		_providerRepository = providerRepository;
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

		var version = await ResolveVersionAsync(request, cancellationToken);

		var seedCriteria = await ResolveSeedCriteriaAsync(request, parsed);

		var downloadId = await client.AddDownloadAsync(releaseInfo, seedCriteria, cancellationToken);

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
			MediaVersionId = version?.Id,
			EpisodeIds = request.EpisodeIds?.ToList() ?? new List<int>()
		};

		await _context.TrackedDownloads.AddAsync(tracked, cancellationToken);
		await _context.SaveChangesAsync(cancellationToken);

		var data = new Dictionary<string, string>
		{
			["downloadClient"] = config.Name,
			["downloadId"] = downloadId,
			["protocol"] = request.Protocol.ToString(),
			["indexer"] = request.Indexer ?? ""
		};

		if (version != null)
			data["version"] = version.Name;

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.GRABBED,
			SeriesId = request.SeriesId,
			EpisodeId = request.EpisodeIds is { Count: 1 } single ? single[0] : null,
			MovieId = request.MovieId,
			SourceTitle = request.Title,
			Quality = parsed.Quality,
			Languages = languages,
			Data = data
		}, cancellationToken);

		if (version != null)
			await _eventPublisher.PublishAsync(
				new MediaGrabbedEvent(request.SeriesId, request.MovieId, version.Path, request.Title),
				cancellationToken);

		return tracked;
	}

	private async Task<SeedCriteria?> ResolveSeedCriteriaAsync(GrabReleaseRequest request, BaseRelease parsed)
	{
		if (request.Protocol != Protocol.BITTORRENT || request.Indexer == null)
			return null;

		var provider = await _providerRepository.FirstByConditionAsync(p => p.Name == request.Indexer);

		var (seedRatio, seedTime, seasonPackSeedTime) = provider switch
		{
			TorznabIndexer indexer => (indexer.SeedRatio, indexer.SeedTime, indexer.SeasonPackSeedTime),
			BittorrentTracker tracker => (tracker.SeedRatio, tracker.SeedTime, tracker.SeasonPackSeedTime),
			_ => ((float?)null, (long?)null, (long?)null)
		};

		if (seedRatio == null && seedTime == null && seasonPackSeedTime == null)
			return null;

		// season packs seed on the season pack time when the indexer defines one
		var seedTimeMinutes = parsed.SeriesReleaseData?.ReleaseType == SeriesReleaseType.FULL_SEASON
			? seasonPackSeedTime ?? seedTime
			: seedTime;

		return new SeedCriteria(seedRatio, (int?)seedTimeMinutes, (int?)seasonPackSeedTime);
	}

	private async Task<Core.Library.MediaVersion?> ResolveVersionAsync(GrabReleaseRequest request,
		CancellationToken cancellationToken)
	{
		if (request.SeriesId == null && request.MovieId == null)
			return null;

		Core.Library.MediaVersion? version;

		if (request.MediaVersionId != null)
			version = await _context.Versions.AsNoTracking()
				.FirstOrDefaultAsync(v => v.Id == request.MediaVersionId, cancellationToken);
		else if (request.SeriesId != null)
			version = await _context.Versions.AsNoTracking()
				.Where(v => v.SeriesId == request.SeriesId)
				.OrderBy(v => v.Id)
				.FirstOrDefaultAsync(cancellationToken);
		else
			version = await _context.Versions.AsNoTracking()
				.Where(v => v.MovieId == request.MovieId)
				.OrderBy(v => v.Id)
				.FirstOrDefaultAsync(cancellationToken);

		if (version == null)
			throw new BadRequestException("no version found for the requested media");

		return version;
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
