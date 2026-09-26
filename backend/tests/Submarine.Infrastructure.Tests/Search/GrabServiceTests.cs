using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Common;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class GrabServiceTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IIndexerProvider _indexerProvider = Substitute.For<IIndexerProvider>();
	private readonly IDownloadClientFactory _downloadClientFactory = Substitute.For<IDownloadClientFactory>();
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly GrabService _service;

	public GrabServiceTests()
	{
		_db = TestDbFactory.Create(_clock);
		_service = new GrabService(_db, _indexerProvider, _downloadClientFactory, _eventBus, _clock);
	}

	public void Dispose() => _db.Dispose();

	private void AddSeries(int id)
		=> _db.Series.Add(new Series { Id = id, TvdbId = id, Title = "Show", CleanTitle = "show" });

	private void AddMovie(int id)
		=> _db.Movies.Add(new Movie { Id = id, TmdbId = id, Title = "Movie", CleanTitle = "movie" });

	private void AddMediaVersion(int id, int? seriesId, int? movieId)
		=> _db.MediaVersions.Add(new MediaVersion { Id = id, SeriesId = seriesId, MovieId = movieId, Name = "1080p", Path = "x", QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = 1 });

	private static ReleaseCandidate MagnetCandidate(int? indexerId = null)
	{
		var release = new TorrentRelease(new BaseRelease
		{
			FullTitle = "Show.S01E01.1080p.WEB-DL-GROUP",
			Title = "Show",
			ReleaseGroup = "GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Languages = [Language.ENGLISH]
		});
		var info = new ReleaseInfo
		{
			Guid = "guid-1",
			Title = release.FullTitle,
			MagnetUrl = "magnet:?xt=urn:btih:abcdef",
			Size = 123456,
			Protocol = Protocol.BITTORRENT,
			IndexerId = indexerId,
			Indexer = indexerId is null ? null : "Stub"
		};

		return new ReleaseCandidate(release, info, null, 1, null, [7]);
	}

	private static IDownloadClient FakeClient(Protocol protocol, string downloadId = "ABCDEF")
	{
		var client = Substitute.For<IDownloadClient>();
		client.Protocol.Returns(protocol);
		client.AddAsync(Arg.Any<RemoteRelease>(), Arg.Any<SeedCriteria?>(), Arg.Any<CancellationToken>()).Returns(downloadId);
		return client;
	}

	[Fact]
	public async Task GrabAsync_ShouldUseExplicitDownloadClient_WhenIndexerHasOneConfigured()
	{
		AddSeries(2);
		AddMediaVersion(5, 2, null);
		_db.Indexers.Add(new Indexer { Id = 1, Name = "Stub", BaseUrl = "http://x", DownloadClientId = 10 });
		_db.DownloadClients.Add(new DownloadClient { Id = 10, Name = "Explicit", Type = DownloadClientType.QBITTORRENT, Enable = true });
		_db.DownloadClients.Add(new DownloadClient { Id = 11, Name = "Other", Type = DownloadClientType.QBITTORRENT, Enable = true, Priority = 0 });
		await _db.SaveChangesAsync();

		var explicitClient = FakeClient(Protocol.BITTORRENT);
		var otherClient = FakeClient(Protocol.BITTORRENT, "OTHER");
		_downloadClientFactory.Create(DownloadClientType.QBITTORRENT, Arg.Any<string>(), 10, "Explicit").Returns(explicitClient);
		_downloadClientFactory.Create(DownloadClientType.QBITTORRENT, Arg.Any<string>(), 11, "Other").Returns(otherClient);

		var decision = new DownloadDecision(MagnetCandidate(1), true, 100, [], [], 0);

		var outcome = await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: 2, episodeIds: [7], movieId: null);

		var grabbed = outcome.ShouldBeOfType<GrabbedOutcome>();
		grabbed.Download.DownloadClientId.ShouldBe(10);
		grabbed.Download.DownloadId.ShouldBe("ABCDEF");
		await explicitClient.Received(1).AddAsync(Arg.Any<RemoteRelease>(), Arg.Any<SeedCriteria?>(), Arg.Any<CancellationToken>());
		await otherClient.DidNotReceive().AddAsync(Arg.Any<RemoteRelease>(), Arg.Any<SeedCriteria?>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GrabAsync_ShouldSelectClientByProtocolAndPriority_WhenNoExplicitClientConfigured()
	{
		AddSeries(2);
		AddMediaVersion(5, 2, null);
		_db.DownloadClients.Add(new DownloadClient { Id = 20, Name = "Usenet", Type = DownloadClientType.SABNZBD, Enable = true, Priority = 1 });
		_db.DownloadClients.Add(new DownloadClient { Id = 21, Name = "TorrentLow", Type = DownloadClientType.QBITTORRENT, Enable = true, Priority = 5 });
		_db.DownloadClients.Add(new DownloadClient { Id = 22, Name = "TorrentHigh", Type = DownloadClientType.TRANSMISSION, Enable = true, Priority = 1 });
		await _db.SaveChangesAsync();

		var usenetClient = FakeClient(Protocol.USENET);
		var lowClient = FakeClient(Protocol.BITTORRENT, "LOW");
		var highClient = FakeClient(Protocol.BITTORRENT, "HIGH");
		_downloadClientFactory.Create(DownloadClientType.SABNZBD, Arg.Any<string>(), 20, "Usenet").Returns(usenetClient);
		_downloadClientFactory.Create(DownloadClientType.QBITTORRENT, Arg.Any<string>(), 21, "TorrentLow").Returns(lowClient);
		_downloadClientFactory.Create(DownloadClientType.TRANSMISSION, Arg.Any<string>(), 22, "TorrentHigh").Returns(highClient);

		var decision = new DownloadDecision(MagnetCandidate(), true, 100, [], [], 0);

		var outcome = await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: 2, episodeIds: [7], movieId: null);

		var grabbed = outcome.ShouldBeOfType<GrabbedOutcome>();
		grabbed.Download.DownloadClientId.ShouldBe(22);
		grabbed.Download.DownloadId.ShouldBe("HIGH");
	}

	[Fact]
	public async Task GrabAsync_ShouldWriteTrackedDownloadHistoryAndPublishEvent_OnApprovedGrab()
	{
		AddSeries(2);
		AddMediaVersion(5, 2, null);
		_db.DownloadClients.Add(new DownloadClient { Id = 30, Name = "Client", Type = DownloadClientType.QBITTORRENT, Enable = true });
		await _db.SaveChangesAsync();
		var client = FakeClient(Protocol.BITTORRENT);
		_downloadClientFactory.Create(DownloadClientType.QBITTORRENT, Arg.Any<string>(), 30, "Client").Returns(client);

		var decision = new DownloadDecision(MagnetCandidate(), true, 100, [], [], 42);

		await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: 2, episodeIds: [7], movieId: null);

		_db.TrackedDownloads.Single().DownloadId.ShouldBe("ABCDEF");
		_db.HistoryEvents.Single().Type.ShouldBe(HistoryEventType.GRABBED);
		_db.HistoryEvents.Single().SeriesId.ShouldBe(2);
		await _eventBus.Received(1).PublishAsync(
			Arg.Is<ReleaseGrabbedEvent>(e => e.Release.DownloadId == "ABCDEF" && e.Release.CustomFormatScore == 42),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GrabAsync_ShouldStorePendingRelease_WhenEveryRejectionIsTemporary()
	{
		AddSeries(2);
		await _db.SaveChangesAsync();

		var decision = new DownloadDecision(
			MagnetCandidate(),
			false,
			0,
			[new RejectionReason("waiting for delay window", RejectionType.TEMPORARY)],
			[],
			0);

		var outcome = await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: 2, episodeIds: [7], movieId: null);

		var pending = outcome.ShouldBeOfType<PendingOutcome>();
		pending.Pending.Reason.ShouldBe(PendingReleaseReason.DELAY);
		_db.PendingReleases.Single().SeriesId.ShouldBe(2);
	}

	[Fact]
	public async Task GrabAsync_ShouldStorePendingReleaseWithAvailabilityReason_WhenRejectionMentionsAvailability()
	{
		AddMovie(9);
		await _db.SaveChangesAsync();

		var decision = new DownloadDecision(
			MagnetCandidate(),
			false,
			0,
			[new RejectionReason("minimum availability not met", RejectionType.TEMPORARY)],
			[],
			0);

		var outcome = await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: null, episodeIds: null, movieId: 9);

		outcome.ShouldBeOfType<PendingOutcome>().Pending.Reason.ShouldBe(PendingReleaseReason.AVAILABILITY);
	}

	[Fact]
	public async Task GrabAsync_ShouldNotDuplicatePendingRelease_WhenAlreadyPendingForSameTarget()
	{
		AddSeries(2);
		await _db.SaveChangesAsync();

		var decision = new DownloadDecision(
			MagnetCandidate(),
			false,
			0,
			[new RejectionReason("waiting for delay window", RejectionType.TEMPORARY)],
			[],
			0);

		await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: 2, episodeIds: [7], movieId: null);
		await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: 2, episodeIds: [7], movieId: null);

		_db.PendingReleases.Count().ShouldBe(1);
	}

	[Fact]
	public async Task GrabAsync_ShouldThrow_WhenAnyRejectionIsPermanent()
	{
		var decision = new DownloadDecision(
			MagnetCandidate(),
			false,
			0,
			[new RejectionReason("quality not allowed", RejectionType.PERMANENT)],
			[],
			0);

		await Should.ThrowAsync<ConflictException>(() => _service.GrabAsync(decision, 5, 2, [7], null));
	}

	[Fact]
	public async Task GrabAsync_ShouldThrow_WhenNoDownloadClientMatchesProtocol()
	{
		AddSeries(2);
		_db.DownloadClients.Add(new DownloadClient { Id = 40, Name = "UsenetOnly", Type = DownloadClientType.SABNZBD, Enable = true });
		await _db.SaveChangesAsync();
		var usenetClient = FakeClient(Protocol.USENET);
		_downloadClientFactory.Create(DownloadClientType.SABNZBD, Arg.Any<string>(), 40, "UsenetOnly").Returns(usenetClient);

		var decision = new DownloadDecision(MagnetCandidate(), true, 100, [], [], 0);

		await Should.ThrowAsync<InvalidOperationException>(() => _service.GrabAsync(decision, 5, 2, [7], null));
	}

	[Fact]
	public async Task GrabAsync_ShouldFollowMagnetRedirect_WhenDownloadLinkRedirectsToMagnet()
	{
		AddSeries(2);
		AddMediaVersion(5, 2, null);
		_db.Indexers.Add(new Indexer { Id = 1, Name = "Stub", BaseUrl = "http://x" });
		_db.DownloadClients.Add(new DownloadClient { Id = 10, Name = "Client", Type = DownloadClientType.QBITTORRENT, Enable = true });
		await _db.SaveChangesAsync();

		var client = FakeClient(Protocol.BITTORRENT);
		_downloadClientFactory.Create(DownloadClientType.QBITTORRENT, Arg.Any<string>(), 10, "Client").Returns(client);

		var indexer = Substitute.For<IIndexer>();
		var response = new HttpResponseMessage(System.Net.HttpStatusCode.Found)
		{
			RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://indexer/download/1")
		};
		response.Headers.Location = new Uri("magnet:?xt=urn:btih:abcdef");
		indexer.DownloadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(response);
		_indexerProvider.CreateAsync(Arg.Any<Indexer>(), Arg.Any<CancellationToken>()).Returns(indexer);

		var release = new TorrentRelease(new BaseRelease
		{
			FullTitle = "Show.S01E01.1080p.WEB-DL-GROUP",
			Title = "Show",
			ReleaseGroup = "GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Languages = [Language.ENGLISH]
		});
		var info = new ReleaseInfo
		{
			Guid = "guid-2",
			Title = release.FullTitle,
			DownloadUrl = "http://indexer/download/1",
			Size = 123456,
			Protocol = Protocol.BITTORRENT,
			IndexerId = 1,
			Indexer = "Stub"
		};
		var candidate = new ReleaseCandidate(release, info, null, 2, null, [7]);
		var decision = new DownloadDecision(candidate, true, 100, [], [], 0);

		var outcome = await _service.GrabAsync(decision, mediaVersionId: 5, seriesId: 2, episodeIds: [7], movieId: null);

		var grabbed = outcome.ShouldBeOfType<GrabbedOutcome>();
		grabbed.Download.DownloadClientId.ShouldBe(10);
		await client.Received(1).AddAsync(
			Arg.Is<RemoteRelease>(remote => remote.MagnetUrl == "magnet:?xt=urn:btih:abcdef" && remote.DownloadUrl == null),
			Arg.Any<SeedCriteria?>(),
			Arg.Any<CancellationToken>());
	}
}
