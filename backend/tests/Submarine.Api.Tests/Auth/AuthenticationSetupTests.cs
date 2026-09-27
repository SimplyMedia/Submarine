using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication;
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

public sealed class AuthenticationSetupTests
{
	[Fact]
	public void ForwardDefaultSelector_ShouldUsePrefetchedSnapshot_WithoutLoadingProvider()
	{
		var provider = Substitute.For<IAuthConfigProvider>();
		var context = new DefaultHttpContext
		{
			RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider()
		};
		context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
		context.Items[TrustedForwardedHeadersMiddleware.AuthSnapshotItemKey] =
			new AuthSnapshot(AuthMethod.BASIC, "key", string.Empty, AuthenticationRequiredType.ENABLED, []);
		var options = new PolicySchemeOptions();

		AuthenticationSetup.ConfigurePolicyScheme(options);

		options.ForwardDefaultSelector!(context).ShouldBe(AuthenticationSetup.BasicScheme);
		provider.DidNotReceive().GetSnapshotAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ForwardDefaultSelector_ShouldUseSnapshotPrefetchedByMiddleware()
	{
		var snapshot = new AuthSnapshot(AuthMethod.BASIC, "key", string.Empty,
			AuthenticationRequiredType.ENABLED, ForwardedForResolver.ParseNetworks("10.0.0.0/8"));
		var provider = Substitute.For<IAuthConfigProvider>();
		provider.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
		var context = new DefaultHttpContext
		{
			RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider()
		};
		context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");

		await TrustedForwardedHeadersMiddleware.InvokeAsync(context, _ => Task.CompletedTask);
		var options = new PolicySchemeOptions();
		AuthenticationSetup.ConfigurePolicyScheme(options);

		options.ForwardDefaultSelector!(context).ShouldBe(AuthenticationSetup.BasicScheme);
		await provider.Received(1).GetSnapshotAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public void ForwardDefaultSelector_ShouldIgnoreEmptyApiKeyHeader()
	{
		var context = new DefaultHttpContext();
		context.Request.Headers["X-Api-Key"] = string.Empty;
		context.Items[TrustedForwardedHeadersMiddleware.AuthSnapshotItemKey] =
			new AuthSnapshot(AuthMethod.FORMS, "key", string.Empty, AuthenticationRequiredType.ENABLED, []);
		var options = new PolicySchemeOptions();

		AuthenticationSetup.ConfigurePolicyScheme(options);

		options.ForwardDefaultSelector!(context).ShouldBe(AuthenticationSetup.CookieScheme);
	}

	[Fact]
	public async Task LoginRateLimitPartitionMiddleware_ShouldPartitionByUsername_ForUntrustedForwarding()
	{
		var provider = Substitute.For<IAuthConfigProvider>();
		var context = new DefaultHttpContext
		{
			RequestServices = new ServiceCollection().AddSingleton(provider).BuildServiceProvider()
		};
		context.Request.Path = "/api/v1/auth/login";
		context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
		context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
		context.Items[TrustedForwardedHeadersMiddleware.AuthSnapshotItemKey] =
			new AuthSnapshot(AuthMethod.FORMS, "key", string.Empty, AuthenticationRequiredType.ENABLED, []);
		context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"username\":\"alice\",\"password\":\"secret\"}"));

		await LoginRateLimitPartitionMiddleware.InvokeAsync(context, _ => Task.CompletedTask);

		context.Items[LoginRateLimitPartitionMiddleware.PartitionKeyItemKey].ShouldBe("user:ALICE");
		context.Request.Body.Position.ShouldBe(0);
	}
}
