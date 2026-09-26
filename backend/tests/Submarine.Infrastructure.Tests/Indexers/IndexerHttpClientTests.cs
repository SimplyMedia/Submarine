using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class IndexerRateLimiterTests
{
	[Fact]
	public async Task WaitForSlotAsync_ShouldPassImmediately_WhenFirstRequest()
	{
		var clock = new FakeTimeProvider();
		using var limiter = new IndexerRateLimiter(clock, 2);

		await limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);

		clock.GetUtcNow().ShouldBe(DateTimeOffset.Parse("2000-01-01T00:00:00Z"));
	}

	[Fact]
	public async Task WaitForSlotAsync_ShouldDelay_WhenSecondRequestWithinDelay()
	{
		var clock = new FakeTimeProvider();
		using var limiter = new IndexerRateLimiter(clock, 2);

		await limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);
		var second = limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);
		second.IsCompleted.ShouldBeFalse();

		clock.Advance(TimeSpan.FromSeconds(1.5));
		second.IsCompleted.ShouldBeFalse();

		clock.Advance(TimeSpan.FromSeconds(0.5));
		await second;

		clock.GetUtcNow().ShouldBe(DateTimeOffset.Parse("2000-01-01T00:00:02Z"));
	}

	[Fact]
	public async Task WaitForSlotAsync_ShouldPassImmediately_WhenDelayElapsed()
	{
		var clock = new FakeTimeProvider();
		using var limiter = new IndexerRateLimiter(clock, 2);

		await limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);
		clock.Advance(TimeSpan.FromSeconds(5));
		await limiter.WaitForSlotAsync(TestContext.Current.CancellationToken);

		clock.GetUtcNow().ShouldBe(DateTimeOffset.Parse("2000-01-01T00:00:05Z"));
	}
}

public class FlareSolverrClientTests
{
	[Fact]
	public async Task RequestAsync_ShouldSendCorrectPayloadAndMirrorCookies()
	{
			HttpRequestMessage? captured = null;
		string? capturedBody = null;
		var handler = new StubHandler(request =>
		{
			captured = request;
			capturedBody = request.Content is null ? null : request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
			return FakeResponses.Json("""
				{
				  "status": "ok",
				  "message": "Challenge solved",
				  "solution": {
				    "url": "https://target.example/",
				    "status": 200,
				    "response": "<html>solved</html>",
				    "cookies": [
				      {"name": "cf_clearance", "value": "token", "domain": "target.example", "path": "/", "secure": "True"}
				    ]
				  }
				}
				""");
		});
		var cookies = new System.Net.CookieContainer();

		var client = new FlareSolverrClient(
			new HttpClient(handler),
			"http://flaresolverr.local:8191",
			"session-1",
			30,
			cookies,
			new NullLogger<FlareSolverrClientTests>());

		var response = await client.RequestAsync(
			new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri("https://target.example/search?q=x")),
			null,
			TestContext.Current.CancellationToken);

		captured.ShouldNotBeNull();
		captured!.Method.ShouldBe(System.Net.Http.HttpMethod.Post);
		captured.RequestUri!.ToString().ShouldBe("http://flaresolverr.local:8191/v1");

		capturedBody.ShouldNotBeNull();

		var payload = JsonDocument.Parse(capturedBody);
		payload.RootElement.GetProperty("cmd").GetString().ShouldBe("request.get");
		payload.RootElement.GetProperty("url").GetString().ShouldBe("https://target.example/search?q=x");
		payload.RootElement.GetProperty("session").GetString().ShouldBe("session-1");
		payload.RootElement.GetProperty("maxTimeout").GetInt32().ShouldBe(30000);

		// the sessions.create preamble was sent first
		handler.Requests[0].Body.ShouldContain("sessions.create");

		response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
		(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("<html>solved</html>");
		cookies.GetCookieHeader(new Uri("https://target.example/")).ShouldContain("cf_clearance=token");
	}

	[Fact]
	public async Task RequestAsync_ShouldUseRequestPostAndPostData_WhenPostRequested()
	{
		var handler = new StubHandler(_ => FakeResponses.Json("""
			{"status": "ok", "solution": {"status": 200, "response": "ok", "cookies": []}}
			"""));
		var client = new FlareSolverrClient(
			new HttpClient(handler),
			"http://flaresolverr.local:8191",
			"session-2",
			60,
			new System.Net.CookieContainer(),
			new NullLogger<FlareSolverrClientTests>());

		var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri("https://target.example/login"))
		{
			Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["user"] = "u" })
		};
		await client.RequestAsync(request, null, TestContext.Current.CancellationToken);

		var post = handler.Requests.Last(recorded => recorded.Body.Contains("request.post")).Body;
		var payload = JsonDocument.Parse(post);
		payload.RootElement.GetProperty("cmd").GetString().ShouldBe("request.post");
		payload.RootElement.GetProperty("postData").GetString().ShouldBe("user=u");
	}

	private sealed record RecordedRequest(Uri Uri, string Body);

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		public List<RecordedRequest> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : string.Empty;
			Requests.Add(new RecordedRequest(request.RequestUri!, body));
			return respond(request);
		}
	}
}

public class Socks4ProxyTests
{
	[Fact]
	public void BuildConnectRequest_ShouldEncodeVersionPortAndIp()
	{
		var request = Socks4Proxy.BuildConnectRequest(System.Net.IPAddress.Parse("93.184.216.34"), 443, "user");

		request[0].ShouldBe((byte)4);
		request[1].ShouldBe((byte)1);
		request[2].ShouldBe((byte)(443 >> 8));
		request[3].ShouldBe((byte)(443 & 0xff));
		request[4].ShouldBe((byte)93);
		request[5].ShouldBe((byte)184);
		request[6].ShouldBe((byte)216);
		request[7].ShouldBe((byte)34);
		Encoding.ASCII.GetString(request[8..^1]).ShouldBe("user");
		request[^1].ShouldBe((byte)0);
	}

	[Fact]
	public void BuildConnectRequest_ShouldOmitUser_WhenNoneGiven()
	{
		var request = Socks4Proxy.BuildConnectRequest(System.Net.IPAddress.Loopback, 80, null);
		request.Length.ShouldBe(9);
		Encoding.ASCII.GetString(request[8..^1]).ShouldBe("");
	}
}

public class Socks4ProxyIpv6Tests
{
	[Fact]
	public async Task ConnectAsync_ShouldThrow_WhenTargetResolvesToIpv6Only()
	{
		var proxy = new Socks4Proxy("127.0.0.1", 1, null);

		var exception = await Should.ThrowAsync<IndexerException>(
			() => proxy.ConnectAsync(new DnsEndPoint("::1", 80), TestContext.Current.CancellationToken));
		exception.Message.ShouldContain("IPv6");
	}
}

public class Socks5ProxyTests
{
	[Fact]
	public async Task ConnectAsync_ShouldSucceed_WhenNoAuthRequired()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		var serverTask = RunFakeServerAsync(listener, requireAuth: false, expectedUser: null, expectedPassword: null);

		var proxy = new Socks5Proxy("127.0.0.1", port, null, null);
		using var socket = await proxy.ConnectAsync(new DnsEndPoint("example.com", 443), TestContext.Current.CancellationToken);

		socket.Connected.ShouldBeTrue();
		await serverTask;
	}

	[Fact]
	public async Task ConnectAsync_ShouldAuthenticate_WhenUsernamePasswordProvided()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		var serverTask = RunFakeServerAsync(listener, requireAuth: true, expectedUser: "user", expectedPassword: "pass");

		var proxy = new Socks5Proxy("127.0.0.1", port, "user", "pass");
		using var socket = await proxy.ConnectAsync(new DnsEndPoint("example.com", 443), TestContext.Current.CancellationToken);

		socket.Connected.ShouldBeTrue();
		await serverTask;
	}

	[Fact]
	public async Task ConnectAsync_ShouldThrow_WhenCredentialsRejected()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		var serverTask = RunFakeServerAsync(listener, requireAuth: true, expectedUser: "user", expectedPassword: "wrong-on-purpose");

		var proxy = new Socks5Proxy("127.0.0.1", port, "user", "pass");
		await Should.ThrowAsync<IndexerException>(
			() => proxy.ConnectAsync(new DnsEndPoint("example.com", 443), TestContext.Current.CancellationToken));
		await serverTask;
	}

	private static async Task RunFakeServerAsync(TcpListener listener, bool requireAuth, string? expectedUser, string? expectedPassword)
	{
		using var client = await listener.AcceptTcpClientAsync();
		await using var stream = client.GetStream();

		var greeting = await ReceiveExactAsync(stream, 2);
		var methodCount = greeting[1];
		await ReceiveExactAsync(stream, methodCount);
		await stream.WriteAsync(new byte[] { 5, (byte)(requireAuth ? 2 : 0) });

		if (requireAuth)
		{
			var authHeader = await ReceiveExactAsync(stream, 2);
			var userLength = authHeader[1];
			var userBytes = await ReceiveExactAsync(stream, userLength);
			var passLengthBuf = await ReceiveExactAsync(stream, 1);
			var passBytes = await ReceiveExactAsync(stream, passLengthBuf[0]);
			var ok = Encoding.UTF8.GetString(userBytes) == expectedUser && Encoding.UTF8.GetString(passBytes) == expectedPassword;
			await stream.WriteAsync(new byte[] { 1, (byte)(ok ? 0 : 1) });
			if (!ok)
				return;
		}

		var connectHeader = await ReceiveExactAsync(stream, 4);
		var addressLength = connectHeader[3] switch
		{
			1 => 4,
			4 => 16,
			3 => (await ReceiveExactAsync(stream, 1))[0],
			_ => 0
		};
		await ReceiveExactAsync(stream, addressLength + 2);
		await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 0, 0, 0, 0, 0, 0 });
	}

	private static async Task<byte[]> ReceiveExactAsync(NetworkStream stream, int count)
	{
		var buffer = new byte[count];
		var read = 0;
		while (read < count)
		{
			var chunk = await stream.ReadAsync(buffer.AsMemory(read));
			if (chunk == 0)
				throw new IOException("Connection closed");
			read += chunk;
		}

		return buffer;
	}
}
