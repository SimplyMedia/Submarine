using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Notifications;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Notifications;
using Submarine.Infrastructure.Tests.DownloadClients;
using Xunit;

namespace Submarine.Infrastructure.Tests.Notifications;

/// <summary>
///     Asserts request shapes of the http based senders against a stub handler.
/// </summary>
public sealed class NotificationSenderTests
{
	private static (IHttpClientFactory Factory, StubHttpHandler Stub) CreateFactory(
		Func<HttpRequestMessage, string?, HttpResponseMessage>? responder = null)
	{
		var stub = new StubHttpHandler(responder ?? ((_, _) => StubHttpHandler.Json("{}")));
		var services = new ServiceCollection();
		services.AddHttpClient(NotificationSenderFactory.HttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => stub);
		var provider = services.BuildServiceProvider();
		return (provider.GetRequiredService<IHttpClientFactory>(), stub);
	}

	private static NotificationMessage Message(NotificationEventType eventType = NotificationEventType.IMPORT, string? path = "/media/tv/Some Show/S01E01.mkv")
		=> new(
			eventType,
			"Imported",
			"Some Show - S01E01 [WEBDL-1080p]",
			1,
			null,
			"Some Show",
			2024,
			new QualityModel(new QualityResolutionModel(), new Revision()),
			[Language.ENGLISH],
			"GRP",
			"https://indexer.example",
			"qBittorrent",
			"abc123",
			1_000_000_000,
			path,
			"https://image.example/poster.jpg",
			[new NotificationEpisode(1, 1, "Pilot", "2024-01-01")],
			[new NotificationLink("Info", "https://info.example")]);

	[Fact]
	public async Task Discord_ShouldPostEmbed()
	{
		var (factory, stub) = CreateFactory();
		var sender = new DiscordSender(factory);

		await sender.SendAsync(Message(), """{"webhookUrl":"https://discord/api/webhooks/1","username":"Submarine"}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Method.ShouldBe(HttpMethod.Post);
		request.Url.ShouldBe("https://discord/api/webhooks/1");
		request.Body!.ShouldContain("\"title\":\"Imported\"");
		request.Body!.ShouldContain("\"description\":\"Some Show - S01E01 [WEBDL-1080p]\"");
		request.Body!.ShouldContain("\"username\":\"Submarine\"");
		request.Body!.ShouldContain("GRP");
	}

	[Fact]
	public async Task Discord_ShouldSkipFields_WhenNotConfigured()
	{
		var (factory, stub) = CreateFactory();
		var sender = new DiscordSender(factory);

		await sender.SendAsync(Message(), """{"webhookUrl":"https://discord/api/webhooks/1","importFields":["year"]}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Body!.ShouldNotContain("GRP");
	}

[Fact]
	public async Task Telegram_ShouldCallBotApi()
	{
		var (factory, stub) = CreateFactory();
		var sender = new TelegramSender(factory);

		await sender.SendAsync(Message(), """{"botToken":"123:secret","chatId":"-42","topicId":7,"sendSilently":true}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Method.ShouldBe(HttpMethod.Post);
		request.Url.ShouldBe("https://api.telegram.org/bot123:secret/sendMessage");
		request.Body!.ShouldContain("\"chat_id\":\"-42\"");
		request.Body!.ShouldContain("\"message_thread_id\":7");
		request.Body!.ShouldContain("\"disable_notification\":true");
	}

	[Fact]
	public async Task Slack_ShouldPostTextAndOverrides()
	{
		var (factory, stub) = CreateFactory();
		var sender = new SlackSender(factory);

		await sender.SendAsync(Message(), """{"webhookUrl":"https://hooks.slack.com/1","username":"Sub","icon":":rocket:","channel":"#media"}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Method.ShouldBe(HttpMethod.Post);
		request.Url.ShouldBe("https://hooks.slack.com/1");
		request.Body!.ShouldContain("\"text\":\"Imported\\nSome Show - S01E01 [WEBDL-1080p]\"");
		request.Body!.ShouldContain("\"icon_emoji\":\":rocket:\"");
		request.Body!.ShouldContain("\"channel\":\"#media\"");
	}

	[Fact]
	public async Task Webhook_ShouldSendConfiguredMethod_AuthAndHeaders()
	{
		var (factory, stub) = CreateFactory();
		var sender = new WebhookSender(factory);
		var settings = """
			{"url":"https://hook.example/notify","method":"PUT","username":"user","password":"pass","headers":{"X-Custom":"yes"}}
			""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Method.ShouldBe(HttpMethod.Put);
		request.HasHeader("Authorization", "Basic dXNlcjpwYXNz").ShouldBeTrue();
		request.HasHeader("X-Custom", "yes").ShouldBeTrue();
		request.Body!.ShouldContain("\"eventType\":\"IMPORT\"");
		request.Body!.ShouldContain("\"mediaTitle\":\"Some Show\"");
	}

	[Fact]
	public async Task Pushover_ShouldPostFormPerDevice()
	{
		var (factory, stub) = CreateFactory();
		var sender = new PushoverSender(factory);
		var settings = """{"apiKey":"app","userKey":"user","devices":["phone","tablet"],"priority":2,"retry":30,"expire":300}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		stub.Requests.Count.ShouldBe(2);
		var first = stub.Requests[0];
		first.Url.ShouldBe("https://api.pushover.net/1/messages.json");
		first.Body!.ShouldContain("token=app");
		first.Body!.ShouldContain("user=user");
		first.Body!.ShouldContain("priority=2");
		first.Body!.ShouldContain("retry=30");
		first.Body!.ShouldContain("device=phone");
		stub.Requests[1].Body!.ShouldContain("device=tablet");
	}

	[Fact]
	public async Task Pushbullet_ShouldPushPerDeviceAndChannel()
	{
		var (factory, stub) = CreateFactory();
		var sender = new PushbulletSender(factory);
		var settings = """{"apiKey":"token","deviceIds":["dev1"],"channelTags":["chan1"]}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		stub.Requests.Count.ShouldBe(2);
		stub.Requests[0].Method.ShouldBe(HttpMethod.Post);
		stub.Requests[0].Url.ShouldBe("https://api.pushbullet.com/v2/pushes");
		stub.Requests[0].HasHeader("Access-Token", "token").ShouldBeTrue();
		stub.Requests[0].Body!.ShouldContain("\"device_iden\":\"dev1\"");
		stub.Requests[1].Body!.ShouldContain("\"channel_tag\":\"chan1\"");
	}

	[Fact]
	public async Task Gotify_ShouldPostWithTokenAndPriority()
	{
		var (factory, stub) = CreateFactory();
		var sender = new GotifySender(factory);

		await sender.SendAsync(Message(), """{"server":"https://gotify.example","appToken":"tok","priority":8,"includeSeriesPoster":true}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Method.ShouldBe(HttpMethod.Post);
		request.Url.ShouldBe("https://gotify.example/message?token=tok");
		request.Body!.ShouldContain("\"priority\":8");
		request.Body!.ShouldContain("bigImageUrl");
	}

	[Fact]
	public async Task Ntfy_ShouldPostPerTopic_WithHeadersAndBearerAuth()
	{
		var (factory, stub) = CreateFactory();
		var sender = new NtfySender(factory);
		var settings = """{"serverUrl":"https://ntfy.example","topics":["a","b"],"accessToken":"tok","priority":4,"clickUrl":"https://sub.example"}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		stub.Requests.Count.ShouldBe(2);
		var first = stub.Requests[0];
		first.Method.ShouldBe(HttpMethod.Post);
		first.Url.ShouldBe("https://ntfy.example/a");
		first.HasHeader("X-Title", "Imported").ShouldBeTrue();
		first.HasHeader("X-Priority", "4").ShouldBeTrue();
		first.HasHeader("X-Click", "https://sub.example").ShouldBeTrue();
		first.HasHeader("Authorization", "Bearer tok").ShouldBeTrue();
	}

	[Fact]
	public async Task Ntfy_ShouldUseBasicAuth_WhenUsernameSet()
	{
		var (factory, stub) = CreateFactory();
		var sender = new NtfySender(factory);

		await sender.SendAsync(Message(), """{"topics":["t"],"username":"u","password":"p"}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().HasHeader("Authorization", "Basic dTpw").ShouldBeTrue();
	}

	[Fact]
	public async Task Apprise_ShouldPostStateless()
	{
		var (factory, stub) = CreateFactory();
		var sender = new AppriseSender(factory);

		await sender.SendAsync(Message(), """{"serverUrl":"https://apprise.example","statelessUrls":["json://a","json://b"],"notificationType":"success"}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Method.ShouldBe(HttpMethod.Post);
		request.Url.ShouldBe("https://apprise.example/notify");
		request.Body!.ShouldContain("\"urls\":\"json://a, json://b\"");
		request.Body!.ShouldContain("\"type\":\"success\"");
	}

	[Fact]
	public async Task Apprise_ShouldPostKeyed_WhenConfigurationKeySet()
	{
		var (factory, stub) = CreateFactory();
		var sender = new AppriseSender(factory);

		await sender.SendAsync(Message(), """{"serverUrl":"https://apprise.example/","configurationKey":"submarine"}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldBe("https://apprise.example/notify/submarine");
	}

	[Theory]
	[InlineData("Kodi", """{"host":"kodi","notify":true}""")]
	[InlineData("Telegram", """{"botToken":"123:secret","chatId":"-42"}""")]
	[InlineData("Slack", """{"webhookUrl":"https://hooks.slack.com/1"}""")]
	[InlineData("Pushbullet", """{"apiKey":"token"}""")]
	[InlineData("Gotify", """{"server":"https://gotify.example","appToken":"tok"}""")]
	[InlineData("Ntfy", """{"topics":["topic"]}""")]
	[InlineData("Apprise", """{"serverUrl":"https://apprise.example","statelessUrls":["json://a"]}""")]
	public async Task OlderProviders_ShouldThrowOnFailedHttpResponse(string provider, string settings)
	{
		var (factory, stub) = CreateFactory((_, _) => StubHttpHandler.Json("{}", HttpStatusCode.InternalServerError));
		INotificationSender sender = provider switch
		{
			"Kodi" => new KodiSender(factory),
			"Telegram" => new TelegramSender(factory),
			"Slack" => new SlackSender(factory),
			"Pushbullet" => new PushbulletSender(factory),
			"Gotify" => new GotifySender(factory),
			"Ntfy" => new NtfySender(factory),
			"Apprise" => new AppriseSender(factory),
			_ => throw new ArgumentOutOfRangeException(nameof(provider))
		};

		await Should.ThrowAsync<HttpRequestException>(
			() => sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken));

		stub.Requests.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Kodi_ShouldNotify_ScanAndClean()
	{
		var (factory, stub) = CreateFactory();
		var sender = new KodiSender(factory);
		var settings = """{"host":"kodi","username":"u","password":"p","notify":true,"updateLibrary":true,"cleanLibrary":true,"displayTime":5}""";

		await sender.SendAsync(Message(NotificationEventType.IMPORT), settings, TestContext.Current.CancellationToken);
		await sender.SendAsync(Message(NotificationEventType.DELETE), settings, TestContext.Current.CancellationToken);

		stub.Requests.Count.ShouldBe(5);
		var notification = stub.Requests[0];
		notification.Url.ShouldBe("http://kodi:8080/jsonrpc");
		notification.HasHeader("Authorization", "Basic dTpw").ShouldBeTrue();
		notification.Body!.ShouldContain("\"method\":\"GUI.ShowNotification\"");
		notification.Body!.ShouldContain("\"displaytime\":5000");
		stub.Requests[1].Body!.ShouldContain("\"method\":\"VideoLibrary.Scan\"");
		stub.Requests[2].Body!.ShouldContain("\"method\":\"GUI.ShowNotification\"");
		stub.Requests[3].Body!.ShouldContain("\"method\":\"VideoLibrary.Scan\"");
		stub.Requests[4].Body!.ShouldContain("\"method\":\"VideoLibrary.Clean\"");
	}

	[Fact]
	public async Task Kodi_ShouldScanOnAlwaysUpdate_EvenForOtherEvents()
	{
		var (factory, stub) = CreateFactory();
		var sender = new KodiSender(factory);
		var settings = """{"host":"kodi","updateLibrary":true,"alwaysUpdate":true}""";

		await sender.SendAsync(Message(NotificationEventType.GRAB, path: null), settings, TestContext.Current.CancellationToken);

		stub.Requests.Select(r => r.Body).ShouldContain(body => body!.Contains("VideoLibrary.Scan"));
	}

	[Fact]
	public async Task Plex_ShouldRefreshMatchingSection()
	{
		var (factory, stub) = CreateFactoryWithSections();
		var sender = new PlexSender(factory);
		var settings = """{"host":"plex","authToken":"tok","updateLibrary":true}""";

		await sender.SendAsync(Message(path: "/media/tv/Some Show/S01E01.mkv"), settings, TestContext.Current.CancellationToken);

		stub.Requests.Count.ShouldBe(2);
		stub.Requests[0].Url.ShouldBe("http://plex:32400/library/sections");
		stub.Requests[0].HasHeader("X-Plex-Token", "tok").ShouldBeTrue();
		stub.Requests[1].Url.ShouldStartWith("http://plex:32400/library/sections/2/refresh?path=");
	}

	[Fact]
	public async Task Plex_ShouldMapPaths()
	{
		var (factory, stub) = CreateFactoryWithSections("/volume2/tv");
		var sender = new PlexSender(factory);
		var settings = """{"host":"plex","authToken":"tok","mapFrom":"/media","mapTo":"/volume2"}""";

		await sender.SendAsync(Message(path: "/media/tv/Show/S01E01.mkv"), settings, TestContext.Current.CancellationToken);

		stub.Requests[1].Url.ShouldContain("path=%2Fvolume2%2Ftv%2FShow%2FS01E01.mkv");
	}

	[Fact]
	public async Task Plex_Test_ShouldCallIdentity()
	{
		var (factory, stub) = CreateFactory();
		var sender = new PlexSender(factory);

		await sender.TestAsync("""{"host":"plex","authToken":"tok"}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldBe("http://plex:32400/identity");
	}

	[Fact]
	public async Task Emby_ShouldNotifyAndUpdateLibrary()
	{
		var (factory, stub) = CreateFactory();
		var sender = new EmbySender(factory);
		var settings = """{"host":"emby","apiKey":"key","notify":true,"updateLibrary":true}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		stub.Requests.Count.ShouldBe(2);
		stub.Requests[0].Url.ShouldBe("http://emby:8096/Notifications/Admin");
		stub.Requests[0].HasHeader("X-Emby-Token", "key").ShouldBeTrue();
		stub.Requests[1].Url.ShouldBe("http://emby:8096/Library/Media/Updated");
		stub.Requests[1].Body!.ShouldContain("\"Path\":\"/media/tv/Some Show/S01E01.mkv\"");
	}

	[Fact]
	public async Task Emby_Test_ShouldCallSystemInfo()
	{
		var (factory, stub) = CreateFactory();
		var sender = new EmbySender(factory);

		await sender.TestAsync("""{"host":"emby","apiKey":"key"}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldBe("http://emby:8096/System/Info");
	}

	[Fact]
	public async Task Jellyfin_ShouldUseMediaBrowserAuth()
	{
		var (factory, stub) = CreateFactory();
		var sender = new JellyfinSender(factory);
		var settings = """{"host":"jf","useSsl":true,"port":8920,"apiKey":"key","notify":false,"updateLibrary":true}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://jf:8920/Library/Media/Updated");
		request.HasHeader("Authorization", "MediaBrowser Token=\"key\"").ShouldBeTrue();
	}

	[Fact]
	public async Task Join_ShouldPostApiKeyTitleTextAndPriority()
	{
		var (factory, stub) = CreateFactory();
		var sender = new JoinSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"jk","priority":1}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldContain("apikey=jk");
		request.Url.ShouldContain("title=Imported");
		request.Url.ShouldContain("priority=1");
		request.Url.ShouldContain("deviceId=group.all");
	}

	[Fact]
	public async Task Join_ShouldUseDeviceNames_WhenSet()
	{
		var (factory, stub) = CreateFactory();
		var sender = new JoinSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"jk","deviceNames":"phone,tablet"}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldContain("deviceNames=phone%2Ctablet");
	}

	[Fact]
	public async Task Mailgun_ShouldPostFormWithBasicAuth()
	{
		var (factory, stub) = CreateFactory();
		var sender = new MailgunSender(factory);
		var settings = """{"apiKey":"mg-key","from":"a@b.c","senderDomain":"mg.example.com","recipients":["d@e.f","g@h.i"]}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.mailgun.net/v3/mg.example.com/messages");
		request.HasHeader("Authorization", $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("api:mg-key"))}").ShouldBeTrue();
		request.Body!.ShouldContain("from=a%40b.c");
		request.Body!.ShouldContain("to=d%40e.f");
		request.Body!.ShouldContain("to=g%40h.i");
		request.Body!.ShouldContain("subject=Imported");
	}

	[Fact]
	public async Task Mailgun_ShouldUseEuEndpoint_WhenConfigured()
	{
		var (factory, stub) = CreateFactory();
		var sender = new MailgunSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"k","useEuEndpoint":true,"from":"a@b.c","senderDomain":"d.com","recipients":["e@f.g"]}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldBe("https://api.eu.mailgun.net/v3/d.com/messages");
	}

	[Fact]
	public async Task Notifiarr_ShouldPostWebhookPayload_WithApiKeyHeader()
	{
		var (factory, stub) = CreateFactory();
		var sender = new NotifiarrSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"nr-key"}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://notifiarr.com/api/v1/notification/sonarr");
		request.HasHeader("X-API-Key", "nr-key").ShouldBeTrue();
		request.Body!.ShouldContain("\"eventType\":\"IMPORT\"");
	}

	[Fact]
	public async Task Notifiarr_ShouldRouteMovieEvents_ToRadarrIntegration()
	{
		var (factory, stub) = CreateFactory();
		var sender = new NotifiarrSender(factory);

		await sender.SendAsync(Message() with { SeriesId = null, MovieId = 5 }, """{"apiKey":"nr-key"}""", TestContext.Current.CancellationToken);

		stub.Requests.Single().Url.ShouldBe("https://notifiarr.com/api/v1/notification/radarr");
	}

	[Fact]
	public async Task Prowl_ShouldPostFormWithApiKeyAndPriority()
	{
		var (factory, stub) = CreateFactory();
		var sender = new ProwlSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"pk","priority":2}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.prowlapp.com/publicapi/add");
		request.Body!.ShouldContain("apikey=pk");
		request.Body!.ShouldContain("application=Submarine");
		request.Body!.ShouldContain("priority=2");
	}

	[Fact]
	public async Task Pushcut_ShouldPostToNotificationPath_WithApiKeyHeader()
	{
		var (factory, stub) = CreateFactory();
		var sender = new PushcutSender(factory);

		await sender.SendAsync(Message(), """{"notificationName":"Submarine Alert","apiKey":"pc","timeSensitive":true}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.pushcut.io/v1/notifications/Submarine Alert");
		request.HasHeader("API-Key", "pc").ShouldBeTrue();
		request.Body!.ShouldContain("\"isTimeSensitive\":true");
	}

	[Fact]
	public async Task Pushsafer_ShouldPostFormWithDevicesAndPriority()
	{
		var (factory, stub) = CreateFactory();
		var sender = new PushsaferSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"ps","deviceIds":["111","222"],"priority":0}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://pushsafer.com/api");
		request.Body!.ShouldContain("k=ps");
		request.Body!.ShouldContain("d=111%7C222");
		request.Body!.ShouldNotContain("re=");
	}

	[Fact]
	public async Task Pushsafer_ShouldIncludeRetryAndExpire_ForEmergencyPriority()
	{
		var (factory, stub) = CreateFactory();
		var sender = new PushsaferSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"ps","priority":2,"retry":120,"expire":3600}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Body!.ShouldContain("re=120");
		request.Body!.ShouldContain("ex=3600");
	}

	[Fact]
	public async Task SendGrid_ShouldPostBearerAuthAndPersonalizations()
	{
		var (factory, stub) = CreateFactory();
		var sender = new SendGridSender(factory);

		await sender.SendAsync(Message(), """{"apiKey":"sg","from":"a@b.c","recipients":["d@e.f"]}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.sendgrid.com/v3/mail/send");
		request.HasHeader("Authorization", "Bearer sg").ShouldBeTrue();
		request.Body!.ShouldContain("\"email\":\"d@e.f\"");
		request.Body!.ShouldContain("\"email\":\"a@b.c\"");
	}

	[Fact]
	public async Task Signal_ShouldPostJson_WithBasicAuthWhenConfigured()
	{
		var (factory, stub) = CreateFactory();
		var sender = new SignalSender(factory);
		var settings = """{"host":"signal","port":8080,"senderNumber":"1000","receiverId":"2000","authUsername":"u","authPassword":"p"}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("http://signal:8080/v2/send");
		request.HasHeader("Authorization", "Basic dTpw").ShouldBeTrue();
		request.Body!.ShouldContain("\"number\":\"1000\"");
		request.Body!.ShouldContain("\"recipients\":[\"2000\"]");
	}

	[Fact]
	public async Task Simplepush_ShouldPostFormWithKeyAndEvent()
	{
		var (factory, stub) = CreateFactory();
		var sender = new SimplepushSender(factory);

		await sender.SendAsync(Message(), """{"key":"spk","event":"encpass"}""", TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.simplepush.io/send");
		request.Body!.ShouldContain("key=spk");
		request.Body!.ShouldContain("event=encpass");
	}

	[Fact]
	public async Task SynologyIndexer_ShouldAddFile_OnImport()
	{
		var process = new FakeSynologyIndexerProcess();
		var sender = new SynologyIndexerSender(process);

		await sender.SendAsync(Message(NotificationEventType.IMPORT), """{"updateLibrary":true}""", TestContext.Current.CancellationToken);

		process.Calls.Single().ShouldBe("-a \"/media/tv/Some Show/S01E01.mkv\"");
	}

	[Fact]
	public async Task SynologyIndexer_ShouldDeleteFile_OnDelete()
	{
		var process = new FakeSynologyIndexerProcess();
		var sender = new SynologyIndexerSender(process);

		await sender.SendAsync(Message(NotificationEventType.DELETE), """{"updateLibrary":true}""", TestContext.Current.CancellationToken);

		process.Calls.Single().ShouldBe("-d \"/media/tv/Some Show/S01E01.mkv\"");
	}

	[Fact]
	public async Task SynologyIndexer_ShouldSkip_WhenUpdateLibraryDisabled()
	{
		var process = new FakeSynologyIndexerProcess();
		var sender = new SynologyIndexerSender(process);

		await sender.SendAsync(Message(NotificationEventType.IMPORT), """{"updateLibrary":false}""", TestContext.Current.CancellationToken);

		process.Calls.ShouldBeEmpty();
	}

	[Fact]
	public async Task Twitter_ShouldSignRequest_AndPostStatus()
	{
		var (factory, stub) = CreateFactory();
		var sender = new TwitterSender(factory);
		var settings = """{"consumerKey":"ck","consumerSecret":"cs","accessToken":"at","accessTokenSecret":"ats","directMessage":false}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.twitter.com/1.1/statuses/update.json");
		var auth = request.Headers.Authorization!.ToString();
		auth.ShouldStartWith("OAuth ");
		auth.ShouldContain("oauth_consumer_key=\"ck\"");
		auth.ShouldContain("oauth_signature=");
		request.Body!.ShouldContain("status=");
	}

	[Fact]
	public async Task Twitter_ShouldSendDirectMessage_WhenConfigured()
	{
		var (factory, stub) = CreateFactory();
		var sender = new TwitterSender(factory);
		var settings = """{"consumerKey":"ck","consumerSecret":"cs","accessToken":"at","accessTokenSecret":"ats","directMessage":true,"mention":"someone"}""";

		await sender.SendAsync(Message(), settings, TestContext.Current.CancellationToken);

		var request = stub.Requests.Single();
		request.Url.ShouldBe("https://api.twitter.com/1.1/direct_messages/new.json");
		request.Body!.ShouldContain("screen_name=someone");
	}

	private sealed class FakeSynologyIndexerProcess : ISynologyIndexerProcess
	{
		public List<string> Calls { get; } = [];

		public Task RunAsync(string arguments, bool treatStdOutAsError = true, CancellationToken cancellationToken = default)
		{
			Calls.Add(arguments);
			return Task.CompletedTask;
		}
	}

	private static (IHttpClientFactory Factory, StubHttpHandler Stub) CreateFactoryWithSections(
		string tvLocation = "/media/tv")
		=> CreateFactory((request, _) =>
		{
			var url = request.RequestUri!.ToString();
			return url.EndsWith("/library/sections")
				? StubHttpHandler.Json(
					"""{"MediaContainer":{"Directory":[{"key":"1","Location":[{"path":"/media/movies"}]},{"key":"2","Location":[{"path":"TV_LOCATION"}]}]}}""".Replace("TV_LOCATION", tvLocation))
				: StubHttpHandler.Json("{}");
		});
}
