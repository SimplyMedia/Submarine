using Shouldly;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class DownloadClientFactoryTests
{
	private static IHttpClientFactory HttpClientFactory()
	{
		var services = new ServiceCollection();
		services.AddHttpClient(DownloadClientFactory.HttpClientName);
		return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
	}

	private static IDownloadClientFactory CreateFactory()
		=> new DownloadClientFactory(HttpClientFactory());

	public static TheoryData<DownloadClientType> AllTypes => new()
	{
		DownloadClientType.QBITTORRENT,
		DownloadClientType.TRANSMISSION,
		DownloadClientType.DELUGE,
		DownloadClientType.RTORRENT,
		DownloadClientType.UTORRENT,
		DownloadClientType.ARIA2,
		DownloadClientType.FLOOD,
		DownloadClientType.DOWNLOAD_STATION,
		DownloadClientType.SABNZBD,
		DownloadClientType.NZBGET,
		DownloadClientType.TORRENT_BLACKHOLE,
		DownloadClientType.USENET_BLACKHOLE
	};

	private static readonly Dictionary<DownloadClientType, string> MinimalSettings = new()
	{
		[DownloadClientType.QBITTORRENT] = "{\"host\":\"x.local\"}",
		[DownloadClientType.TRANSMISSION] = "{\"host\":\"x.local\"}",
		[DownloadClientType.DELUGE] = "{\"host\":\"x.local\",\"password\":\"p\"}",
		[DownloadClientType.RTORRENT] = "{\"host\":\"x.local\"}",
		[DownloadClientType.UTORRENT] = "{\"host\":\"x.local\"}",
		[DownloadClientType.ARIA2] = "{\"host\":\"x.local\"}",
		[DownloadClientType.FLOOD] = "{\"host\":\"x.local\",\"username\":\"u\",\"password\":\"p\"}",
		[DownloadClientType.DOWNLOAD_STATION] = "{\"host\":\"x.local\",\"username\":\"u\",\"password\":\"p\"}",
		[DownloadClientType.SABNZBD] = "{\"host\":\"x.local\",\"apiKey\":\"k\"}",
		[DownloadClientType.NZBGET] = "{\"host\":\"x.local\",\"username\":\"u\",\"password\":\"p\"}",
		[DownloadClientType.TORRENT_BLACKHOLE] = "{\"torrentFolder\":\"/t\",\"watchFolder\":\"/w\"}",
		[DownloadClientType.USENET_BLACKHOLE] = "{\"nzbFolder\":\"/n\",\"watchFolder\":\"/w\"}"
	};

	[Theory]
	[MemberData(nameof(AllTypes))]
	public void Create_ShouldReturnClientOfRequestedType_WhenSettingsValid(DownloadClientType type)
	{
		var settings = MinimalSettings[type];
		var factory = CreateFactory();

		var client = factory.Create(type, settings, 1, "name");

		client.Type.ShouldBe(type);
		client.Protocol.ShouldBe(type is DownloadClientType.SABNZBD or DownloadClientType.NZBGET
			or DownloadClientType.USENET_BLACKHOLE
			? Protocol.USENET
			: Protocol.BITTORRENT);
	}

	[Fact]
	public void Create_ShouldThrowWithDetails_WhenSettingsInvalid()
	{
		var factory = CreateFactory();

		var exception = Should.Throw<DownloadClientException>(() =>
			factory.Create(DownloadClientType.DELUGE, "{}", 1, "my-deluge"));

		exception.Message.ShouldContain("my-deluge");
		exception.Message.ShouldContain("host");
		exception.Message.ShouldContain("password");
	}

	[Fact]
	public void Create_ShouldSetPerInstanceBaseAddress()
	{
		var services = new ServiceCollection();
		services.AddHttpClient(DownloadClientFactory.HttpClientName);
		var provider = services.BuildServiceProvider();
		var factory = new DownloadClientFactory(provider.GetRequiredService<IHttpClientFactory>());

		var client = (DownloadClientBase<QBittorrentSettings>)factory.Create(
			DownloadClientType.QBITTORRENT, """{"host":"qb.local","port":8090,"useSsl":true}""", 1, "qb");

		client.Http.BaseAddress.ShouldBe(new Uri("https://qb.local:8090/"));
	}
}
