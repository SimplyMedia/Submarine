using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Download;
using Submarine.Core.History;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.Tests;

public class GrabServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task GrabAsync_ShouldPersistTrackedDownloadAndGrabHistory_WhenClientAcceptsRelease()
	{
		Context.DownloadClients.Add(new DownloadClientConfig
		{
			Name = "qbit", Type = DownloadClientType.QBITTORRENT, Enable = true, Priority = 1, SettingsJson = "{}"
		});
		await Context.SaveChangesAsync();

		var client = new FakeDownloadClient { Protocol = Protocol.BITTORRENT, DownloadId = "hash123" };
		var service = new GrabService(Context, new FakeDownloadClientFactory(client), ReleaseParser(),
			new HistoryService(Context), new FakeEventPublisher());

		var request = new GrabReleaseRequest("The Show S01E01 1080p WEB-DL x264-GROUP", "guid-1",
			"http://indexer/download", "MyIndexer", Protocol.BITTORRENT, 1234, null, null, null);

		var tracked = await service.GrabAsync(request);

		Assert.Equal("hash123", tracked.DownloadId);

		var stored = await Context.TrackedDownloads.SingleAsync();
		Assert.Equal("hash123", stored.DownloadId);
		Assert.Equal(DownloadItemStatus.QUEUED, stored.Status);
		Assert.Equal("MyIndexer", stored.Indexer);
		Assert.Equal("guid-1", client.LastAdded!.Guid);

		var history = await Context.History.SingleAsync();
		Assert.Equal(HistoryEventType.GRABBED, history.Type);
		Assert.Equal("qbit", history.Data["downloadClient"]);
	}

	[Fact]
	public async Task GrabAsync_ShouldThrow_WhenNoEnabledClientForProtocol()
	{
		var client = new FakeDownloadClient { Protocol = Protocol.USENET };
		var service = new GrabService(Context, new FakeDownloadClientFactory(client), ReleaseParser(),
			new HistoryService(Context), new FakeEventPublisher());

		Context.DownloadClients.Add(new DownloadClientConfig
		{
			Name = "sab", Type = DownloadClientType.SABNZBD, Enable = false, Priority = 1, SettingsJson = "{}"
		});
		await Context.SaveChangesAsync();

		var request = new GrabReleaseRequest("The Show S01E01 1080p WEB-DL x264-GROUP", "guid-1", null, null,
			Protocol.BITTORRENT, null, null, null, null);

		await Assert.ThrowsAsync<Exceptions.BadRequestException>(() => service.GrabAsync(request));
	}
}
