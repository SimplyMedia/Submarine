using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Grab;

/// <summary>
///     Default <see cref="IGrabService" />. Resolves a download client, fetches the torrent or nzb payload through
///     the owning indexer when needed, and records the grab across the tracked download, indexer history and library
///     history tables.
/// </summary>
public sealed class GrabService(
	SubmarineDbContext db,
	IIndexerProvider indexerProvider,
	IDownloadClientFactory downloadClientFactory,
	IEventBus eventBus,
	TimeProvider timeProvider) : IGrabService
{
	/// <inheritdoc />
	public async Task<GrabOutcome> GrabAsync(
		DownloadDecision decision,
		int mediaVersionId,
		int? seriesId,
		IReadOnlyList<int>? episodeIds,
		int? movieId,
		CancellationToken cancellationToken = default)
	{
		if (decision.Approved)
		{
			return new GrabbedOutcome(await GrabApprovedAsync(decision, mediaVersionId, seriesId, episodeIds, movieId, cancellationToken));
		}

		if (decision.Rejections.Count > 0 && decision.Rejections.All(rejection => rejection.Type == RejectionType.TEMPORARY))
		{
			return new PendingOutcome(await StorePendingAsync(decision, seriesId, episodeIds, movieId, cancellationToken));
		}

		throw new Core.Common.ConflictException($"'{decision.Candidate.Parsed.FullTitle}' is rejected and cannot be grabbed");
	}

	private async Task<TrackedDownload> GrabApprovedAsync(
		DownloadDecision decision,
		int mediaVersionId,
		int? seriesId,
		IReadOnlyList<int>? episodeIds,
		int? movieId,
		CancellationToken cancellationToken)
	{
		var release = decision.Candidate.Info;
		var parsed = decision.Candidate.Parsed;

		var (client, clientEntity) = await ResolveDownloadClientAsync(release, cancellationToken)
			?? throw new InvalidOperationException("No enabled download client matches this release's protocol");

		byte[]? torrentFile = null;
		byte[]? nzbFile = null;
		var magnetUrl = release.MagnetUrl;

		if (release.Protocol == Protocol.USENET && release.DownloadUrl is { } nzbUrl && release.IndexerId is { } nzbIndexerId)
		{
			(nzbFile, _) = await FetchAsync(nzbIndexerId, nzbUrl, cancellationToken);
		}
		else if (release.Protocol == Protocol.BITTORRENT && magnetUrl is null
			&& release.DownloadUrl is { } torrentUrl && release.IndexerId is { } torrentIndexerId)
		{
			(torrentFile, magnetUrl) = await FetchAsync(torrentIndexerId, torrentUrl, cancellationToken);
		}

		var indexerEntity = release.IndexerId is { } indexerId
			? await db.Indexers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == indexerId, cancellationToken)
			: null;

		var seedCriteria = release.Protocol == Protocol.BITTORRENT && indexerEntity is not null
			? new SeedCriteria(indexerEntity.SeedRatio, indexerEntity.SeedTimeMinutes, indexerEntity.SeasonPackSeedTimeMinutes)
			: null;

		var isSeasonPack = (episodeIds?.Count ?? 0) > 1
			|| parsed.SeriesReleaseData?.ReleaseType is SeriesReleaseType.FULL_SEASON or SeriesReleaseType.MULTI_SEASON;

		var remoteRelease = new RemoteRelease
		{
			Title = parsed.FullTitle,
			DownloadUrl = magnetUrl is null ? release.DownloadUrl : null,
			MagnetUrl = magnetUrl,
			InfoHash = release.InfoHash,
			Size = release.Size ?? 0,
			Protocol = release.Protocol,
			IsSeasonPack = isSeasonPack,
			Category = movieId is not null ? RemoteReleaseCategory.MOVIE : RemoteReleaseCategory.SERIES,
			TorrentFile = torrentFile,
			NzbFile = nzbFile
		};

		var downloadId = await client.AddAsync(remoteRelease, seedCriteria, cancellationToken);
		var now = timeProvider.GetUtcNow().UtcDateTime;

		var tracked = new TrackedDownload
		{
			DownloadClientId = clientEntity.Id,
			DownloadId = downloadId,
			Title = parsed.FullTitle,
			Protocol = release.Protocol,
			Status = TrackedDownloadStatus.QUEUED,
			State = TrackedDownloadState.DOWNLOADING,
			ReleaseTitle = parsed.FullTitle,
			Quality = parsed.Quality,
			Languages = [.. parsed.Languages],
			ReleaseGroup = parsed.ReleaseGroup,
			IndexerId = release.IndexerId,
			SeriesId = seriesId,
			MovieId = movieId,
			MediaVersionId = mediaVersionId,
			EpisodeIds = [.. episodeIds ?? []],
			Size = release.Size ?? 0,
			SizeLeft = release.Size ?? 0,
			Added = now
		};

		db.TrackedDownloads.Add(tracked);

		if (release.IndexerId is { } grabbedIndexerId)
		{
			db.IndexerHistories.Add(new IndexerHistory
			{
				IndexerId = grabbedIndexerId,
				EventType = IndexerHistoryEventType.GRAB,
				Successful = true,
				Query = parsed.FullTitle,
				Source = "grab",
				Date = now
			});
		}

		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = HistoryEventType.GRABBED,
			SeriesId = seriesId,
			MovieId = movieId,
			MediaVersionId = mediaVersionId,
			SourceTitle = parsed.FullTitle,
			Quality = parsed.Quality,
			Languages = [.. parsed.Languages],
			DownloadId = downloadId,
			Date = now
		});

		await db.SaveChangesAsync(cancellationToken);

		await eventBus.PublishAsync(new ReleaseGrabbedEvent(new GrabbedRelease(
			parsed.FullTitle,
			release.Indexer,
			release.IndexerId,
			clientEntity.Id,
			clientEntity.Name,
			downloadId,
			release.Protocol,
			release.Size ?? 0,
			parsed.Quality,
			parsed.Languages,
			parsed.ReleaseGroup,
			seriesId,
			episodeIds ?? [],
			movieId,
			mediaVersionId,
			release.Guid,
			release.InfoUrl,
			decision.CustomFormatScore)), cancellationToken);

		return tracked;
	}

	private async Task<PendingRelease> StorePendingAsync(
		DownloadDecision decision,
		int? seriesId,
		IReadOnlyList<int>? episodeIds,
		int? movieId,
		CancellationToken cancellationToken)
	{
		var parsed = decision.Candidate.Parsed;
		var reason = decision.Rejections.Any(rejection => rejection.Reason.Contains("availability", StringComparison.OrdinalIgnoreCase))
			? PendingReleaseReason.AVAILABILITY
			: PendingReleaseReason.DELAY;

		var existing = await db.PendingReleases.FirstOrDefaultAsync(
			pending => pending.Title == parsed.FullTitle && pending.SeriesId == seriesId && pending.MovieId == movieId,
			cancellationToken);

		if (existing is not null)
		{
			return existing;
		}

		var pending = new PendingRelease
		{
			Title = parsed.FullTitle,
			Release = JsonSerializer.Serialize(decision.Candidate.Info),
			SeriesId = seriesId,
			MovieId = movieId,
			EpisodeIds = [.. episodeIds ?? []],
			Reason = reason,
			Added = timeProvider.GetUtcNow().UtcDateTime
		};

		db.PendingReleases.Add(pending);
		await db.SaveChangesAsync(cancellationToken);
		return pending;
	}

	// Torznab/Jackett-style proxies 302 a download link straight to a magnet: URI, which HttpClient cannot fetch as
	// bytes; hand that magnet URI to the download client instead of the (unfetchable) torrent file
	private async Task<(byte[]? File, string? MagnetUrl)> FetchAsync(int indexerId, string url, CancellationToken cancellationToken)
	{
		var indexerEntity = await db.Indexers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == indexerId, cancellationToken)
			?? throw new KeyNotFoundException($"Indexer {indexerId} not found");

		await using var indexer = await indexerProvider.CreateAsync(indexerEntity, cancellationToken);
		var response = await indexer.DownloadAsync(new Uri(url), cancellationToken);

		if ((int)response.StatusCode is >= 300 and < 400
		    && response.Headers.Location is { } location
		    && location.Scheme.Equals("magnet", StringComparison.OrdinalIgnoreCase))
		{
			return (null, location.ToString());
		}

		response.EnsureSuccessStatusCode();
		return (await response.Content.ReadAsByteArrayAsync(cancellationToken), null);
	}

	private async Task<(IDownloadClient Client, DownloadClient Entity)?> ResolveDownloadClientAsync(
		Core.Indexers.ReleaseInfo release,
		CancellationToken cancellationToken)
	{
		Indexer? indexer = release.IndexerId is { } indexerId
			? await db.Indexers.Include(entity => entity.Tags).AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == indexerId, cancellationToken)
			: null;

		if (indexer?.DownloadClientId is { } explicitId)
		{
			var explicitEntity = await db.DownloadClients.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == explicitId && entity.Enable, cancellationToken);
			if (explicitEntity is not null)
			{
				return (downloadClientFactory.Create(explicitEntity.Type, explicitEntity.SettingsJson, explicitEntity.Id, explicitEntity.Name), explicitEntity);
			}
		}

		var indexerTagIds = indexer?.Tags.Select(tag => tag.Id).ToHashSet() ?? [];
		var candidates = await db.DownloadClients.Include(entity => entity.Tags).AsNoTracking()
			.Where(entity => entity.Enable)
			.OrderBy(entity => entity.Priority)
			.ToListAsync(cancellationToken);

		var matching = candidates
			.Select(entity => (Entity: entity, Client: TryCreate(entity)))
			.Where(pair => pair.Client is { } client && client.Protocol == release.Protocol)
			.OrderByDescending(pair => pair.Entity.Tags.Any(tag => indexerTagIds.Contains(tag.Id)))
			.ThenBy(pair => pair.Entity.Priority)
			.ToList();

		return matching.Count == 0 ? null : (matching[0].Client!, matching[0].Entity);
	}

	private IDownloadClient? TryCreate(DownloadClient entity)
	{
		try
		{
			return downloadClientFactory.Create(entity.Type, entity.SettingsJson, entity.Id, entity.Name);
		}
		catch (DownloadClientException)
		{
			return null;
		}
	}
}
