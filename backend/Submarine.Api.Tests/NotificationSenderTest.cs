using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Core.Notification;
using Xunit;

namespace Submarine.Api.Tests;

public class NotificationSenderTest
{
	private static readonly NotificationMessage ImportMessage =
		new("import", "Movie Title", "/movies/movie", null, DateTimeOffset.UtcNow);

	[Fact]
	public async Task SendAsync_ShouldPostTextToWebhookUrl_WhenSlack()
	{
		var (client, stub) = NewClient();
		var sender = new SlackNotificationSender(client,
			new SlackConnection { WebhookUrl = "https://hooks.slack.com/services/x" });

		await sender.SendAsync(ImportMessage);

		var request = Assert.Single(stub.Requests);
		Assert.Equal("https://hooks.slack.com/services/x", request.RequestUri!.ToString());
		Assert.Contains("\"text\":\"Imported: Movie Title\"", Assert.Single(stub.Bodies));
	}

	[Fact]
	public async Task SendAsync_ShouldPostForm_WhenPushover()
	{
		var (client, stub) = NewClient();
		var sender = new PushoverNotificationSender(client,
			new PushoverConnection { AppToken = "app-token", UserKey = "user-key" });

		await sender.SendAsync(ImportMessage);

		var request = Assert.Single(stub.Requests);
		Assert.Equal("https://api.pushover.net/1/messages.json", request.RequestUri!.ToString());

		var body = Assert.Single(stub.Bodies);
		Assert.Contains("token=app-token", body);
		Assert.Contains("user=user-key", body);
		Assert.Contains("title=Submarine", body);
	}

	[Fact]
	public async Task SendAsync_ShouldPostNoteWithAccessTokenHeader_WhenPushbullet()
	{
		var (client, stub) = NewClient();
		var sender = new PushbulletNotificationSender(client,
			new PushbulletConnection { AccessToken = "access-token" });

		await sender.SendAsync(ImportMessage);

		var request = Assert.Single(stub.Requests);
		Assert.Equal("https://api.pushbullet.com/v2/pushes", request.RequestUri!.ToString());
		Assert.Equal("access-token", Assert.Single(request.Headers.GetValues("Access-Token")));

		var body = Assert.Single(stub.Bodies);
		Assert.Contains("\"type\":\"note\"", body);
		Assert.Contains("\"body\":\"Imported: Movie Title\"", body);
	}

	[Fact]
	public async Task SendAsync_ShouldPostToServerUrlWithToken_WhenGotify()
	{
		var (client, stub) = NewClient();
		var sender = new GotifyNotificationSender(client,
			new GotifyConnection { ServerUrl = "https://gotify.example/", AppToken = "app-token" });

		await sender.SendAsync(ImportMessage);

		var request = Assert.Single(stub.Requests);
		Assert.Equal("https://gotify.example/message?token=app-token", request.RequestUri!.ToString());

		var body = Assert.Single(stub.Bodies);
		Assert.Contains("\"message\":\"Imported: Movie Title\"", body);
		Assert.Contains("\"priority\":5", body);
	}

	[Fact]
	public async Task SendAsync_ShouldShowNotificationAndScanLibraryWithBasicAuth_WhenKodiImport()
	{
		var (client, stub) = NewClient();
		var sender = new KodiNotificationSender(client, new KodiConnection
		{
			Host = "kodi.local", Port = 8080, Username = "user", Password = "pass"
		});

		await sender.SendAsync(ImportMessage);

		Assert.Equal(2, stub.Requests.Count);
		Assert.All(stub.Requests, r => Assert.Equal("http://kodi.local:8080/jsonrpc", r.RequestUri!.ToString()));
		Assert.All(stub.Requests, r => Assert.Equal("Basic", r.Headers.Authorization!.Scheme));
		Assert.Contains("\"method\":\"GUI.ShowNotification\"", stub.Bodies[0]);
		Assert.Contains("\"method\":\"VideoLibrary.Scan\"", stub.Bodies[1]);
	}

	[Fact]
	public async Task SendAsync_ShouldNotScanLibrary_WhenKodiEventIsNotImportOrUpgrade()
	{
		var (client, stub) = NewClient();
		var sender = new KodiNotificationSender(client, new KodiConnection { Host = "kodi.local", Port = 8080 });

		await sender.SendAsync(new NotificationMessage("grab", "Movie Title", null, null, DateTimeOffset.UtcNow));

		var request = Assert.Single(stub.Requests);
		Assert.Null(request.Headers.Authorization);
		Assert.Contains("\"method\":\"GUI.ShowNotification\"", Assert.Single(stub.Bodies));
	}

	[Fact]
	public void BuildStartInfo_ShouldSetEnvironmentVariablesWithoutShell_WhenCustomScript()
	{
		var sender = new CustomScriptNotificationSender(new CustomScriptConnection { ScriptPath = "/scripts/notify.sh" },
			NullLogger<CustomScriptNotificationSender>.Instance);

		var startInfo = sender.BuildStartInfo(new NotificationMessage("import", "Movie Title", "/movies/movie", null,
			DateTimeOffset.UtcNow, SeriesId: null, MovieId: 7));

		Assert.Equal("/scripts/notify.sh", startInfo.FileName);
		Assert.False(startInfo.UseShellExecute);
		Assert.Equal("", startInfo.Arguments);
		Assert.Equal("import", startInfo.EnvironmentVariables["SUBMARINE_EVENT_TYPE"]);
		Assert.Equal("Movie Title", startInfo.EnvironmentVariables["SUBMARINE_MEDIA_TITLE"]);
		Assert.Equal("/movies/movie", startInfo.EnvironmentVariables["SUBMARINE_MEDIA_PATH"]);
		Assert.Equal("", startInfo.EnvironmentVariables["SUBMARINE_SERIES_ID"]);
		Assert.Equal("7", startInfo.EnvironmentVariables["SUBMARINE_MOVIE_ID"]);
	}

	[Fact]
	public async Task RunAsync_ShouldComplete_WhenProcessExits()
	{
		if (!OperatingSystem.IsWindows())
			return;

		var sender = new CustomScriptNotificationSender(new CustomScriptConnection { ScriptPath = "cmd.exe" },
			NullLogger<CustomScriptNotificationSender>.Instance);

		await sender.RunAsync(new ProcessStartInfo("cmd.exe", "/c exit 0")
		{
			UseShellExecute = false, CreateNoWindow = true
		}, CancellationToken.None);
	}

	private static (HttpClient Client, StubHttpMessageHandler Stub) NewClient()
	{
		var stub = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");

		return (new HttpClient(stub), stub);
	}
}
