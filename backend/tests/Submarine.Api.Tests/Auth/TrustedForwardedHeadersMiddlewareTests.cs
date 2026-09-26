using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Submarine.Api.Auth;
using Submarine.Core.Enums;
using Submarine.Core.Net;
using Submarine.Infrastructure.Auth;
using Xunit;

namespace Submarine.Api.Tests.Auth;

public sealed class TrustedForwardedHeadersMiddlewareTests
{
	private static HttpContext CreateContext(IPAddress peer, string? forwardedFor, AuthSnapshot snapshot)
	{
		var provider = Substitute.For<IAuthConfigProvider>();
		provider.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);

		var context = new DefaultHttpContext
		{
			RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider()
		};
		context.Connection.RemoteIpAddress = peer;
		if (forwardedFor is not null)
		{
			context.Request.Headers["X-Forwarded-For"] = forwardedFor;
		}

		return context;
	}

	private static AuthSnapshot Snapshot(string trustedProxies)
		=> new(AuthMethod.FORMS, "key", string.Empty, AuthenticationRequiredType.ENABLED, ForwardedForResolver.ParseNetworks(trustedProxies));

	[Fact]
	public async Task InvokeAsync_ShouldIgnoreForwardedFor_FromUntrustedPeer()
	{
		var context = CreateContext(IPAddress.Parse("203.0.113.9"), "10.0.0.1", Snapshot("10.0.0.0/8"));

		await TrustedForwardedHeadersMiddleware.InvokeAsync(context, _ => Task.CompletedTask);

		context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("203.0.113.9"));
	}

	[Fact]
	public async Task InvokeAsync_ShouldResolveRealClient_WhenForwardedByTrustedProxy()
	{
		var context = CreateContext(IPAddress.Parse("10.0.0.5"), "203.0.113.9", Snapshot("10.0.0.0/8"));

		await TrustedForwardedHeadersMiddleware.InvokeAsync(context, _ => Task.CompletedTask);

		context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("203.0.113.9"));
	}

	[Fact]
	public async Task InvokeAsync_ShouldLeavePeerUnchanged_WhenNoTrustedProxiesConfigured()
	{
		var context = CreateContext(IPAddress.Parse("10.0.0.5"), "203.0.113.9", Snapshot(""));

		await TrustedForwardedHeadersMiddleware.InvokeAsync(context, _ => Task.CompletedTask);

		context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("10.0.0.5"));
	}

	[Fact]
	public async Task InvokeAsync_ShouldCallNext()
	{
		var context = CreateContext(IPAddress.Parse("127.0.0.1"), null, Snapshot(""));
		var called = false;

		await TrustedForwardedHeadersMiddleware.InvokeAsync(context, _ =>
		{
			called = true;
			return Task.CompletedTask;
		});

		called.ShouldBeTrue();
	}
}
