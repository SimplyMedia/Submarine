using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Infrastructure.Downloads;
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
	IDownloadClientStatusTracker statusTracker,
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

		var mediaTagIds = seriesId is { } seriesTagId
			? await db.Series.AsNoTracking().Where(series => series.Id == seriesTagId)
				.SelectMany(series => series.Tags.Select(tag => tag.Id)).ToListAsync(cancellationToken)
			: movieId is { } movieTagId
				? await db.Movies.AsNoTracking().Where(movie => movie.Id == movieTagId)
					.SelectMany(movie => movie.Tags.Select(tag => tag.Id)).ToListAsync(cancellationToken)
				: [];

		var (client, clientEntity) = await ResolveDownloadClientAsync(release, mediaTagIds, cancellationToken)
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

		var (isRecentRelease, year, network) = await GetReleaseMetadataAsync(seriesId, episodeIds, movieId, cancellationToken);

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
			NzbFile = nzbFile,
			IsRecentRelease = isRecentRelease,
			ReleaseGroup = parsed.ReleaseGroup,
			Quality = $"{parsed.Quality.Resolution.Source}-{parsed.Quality.Resolution.Resolution}",
			Languages = [.. parsed.Languages.Select(language => ToTitleCase(language.ToString()))],
			Indexer = release.Indexer,
			Year = year,
			Network = network
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

	/// <summary>
	///     Resolves the download client for a release: an indexer-pinned client when configured (matching the
	///     release's protocol), otherwise the enabled clients of that protocol whose tags intersect
	///     <paramref name="mediaTagIds" /> (or, when none match, the untagged clients), preferring clients that
	///     are not currently backed off and round-robining by id within the highest-priority (lowest value) group
	/// </summary>
	private async Task<(IDownloadClient Client, DownloadClient Entity)?> ResolveDownloadClientAsync(
		Core.Indexers.ReleaseInfo release,
		IReadOnlyCollection<int> mediaTagIds,
		CancellationToken cancellationToken)
	{
		var rows = await db.DownloadClients.Include(entity => entity.Tags).AsNoTracking()
			.Where(entity => entity.Enable)
			.ToListAsync(cancellationToken);

		var matchingProtocol = rows
			.Select(entity => (Entity: entity, Client: TryCreate(entity)))
			.Where(pair => pair.Client is { } client && client.Protocol == release.Protocol)
			.ToList();

		if (matchingProtocol.Count == 0)
			return null;

		var explicitId = release.IndexerId is { } indexerId
			? (await db.Indexers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == indexerId, cancellationToken))
				?.DownloadClientId
			: null;

		if (explicitId is { } id)
		{
			var explicitMatch = matchingProtocol.FirstOrDefault(pair => pair.Entity.Id == id);
			return explicitMatch.Entity is null ? null : (explicitMatch.Client!, explicitMatch.Entity);
		}

		var tagSet = mediaTagIds.ToHashSet();
		var matchingTags = matchingProtocol.Where(pair => pair.Entity.Tags.Any(tag => tagSet.Contains(tag.Id))).ToList();
		var candidates = matchingTags.Count > 0
			? matchingTags
			: matchingProtocol.Where(pair => pair.Entity.Tags.Count == 0).ToList();

		if (candidates.Count == 0)
			return null;

		var available = candidates.Where(pair => statusTracker.IsAvailable(pair.Entity.Id)).ToList();
		if (available.Count > 0)
			candidates = available;

		var bestPriority = candidates.Min(pair => pair.Entity.Priority);
		var group = candidates.Where(pair => pair.Entity.Priority == bestPriority)
			.OrderBy(pair => pair.Entity.Id)
			.ToList();

		var lastId = statusTracker.GetLastUsedClientId(release.Protocol);
		var next = group.FirstOrDefault(pair => pair.Entity.Id > lastId);
		var selected = next.Entity is null ? group[0] : next;

		statusTracker.SetLastUsedClientId(release.Protocol, selected.Entity.Id);

		return (selected.Client!, selected.Entity);
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

	/// <summary>
	///     Resolves the media metadata download clients can key their behaviour on: whether the release is
	///     recent (a series episode that aired within the last 14 days, or a movie whose physical/digital
	///     release was within the last 21 days or cinema release within the last 120 days), the media's
	///     first-air or release year, and the series' broadcast network (null for movies)
	/// </summary>
	private async Task<(bool IsRecent, int? Year, string? Network)> GetReleaseMetadataAsync(int? seriesId,
		IReadOnlyList<int>? episodeIds, int? movieId, CancellationToken cancellationToken)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;

		if (seriesId is { } id)
		{
			var series = await db.Series.AsNoTracking().Where(entity => entity.Id == id)
				.Select(entity => new { entity.Year, entity.Network })
				.FirstOrDefaultAsync(cancellationToken);

			var isRecent = episodeIds is { Count: > 0 } && await db.Episodes.AsNoTracking()
				.Where(episode => episodeIds.Contains(episode.Id))
				.AnyAsync(episode => episode.AirDateUtc != null && episode.AirDateUtc >= now.AddDays(-14),
					cancellationToken);

			return (isRecent, series?.Year, series?.Network);
		}

		if (movieId is { } id2)
		{
			var movie = await db.Movies.AsNoTracking().Where(entity => entity.Id == id2)
				.Select(entity => new
				{
					entity.Year,
					entity.PhysicalReleaseDate,
					entity.DigitalReleaseDate,
					entity.InCinemasDate
				})
				.FirstOrDefaultAsync(cancellationToken);

			if (movie is null)
				return (false, null, null);

			var isRecent = (movie.PhysicalReleaseDate is { } physical && physical >= now.AddDays(-21))
				|| (movie.DigitalReleaseDate is { } digital && digital >= now.AddDays(-21))
				|| (movie.InCinemasDate is { } cinemas && cinemas >= now.AddDays(-120));

			return (isRecent, movie.Year, null);
		}

		return (false, null, null);
	}

	private static string ToTitleCase(string value)
		=> value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
