using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Submarine.Api.Auth;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Auth;
using Xunit;

namespace Submarine.Api.Tests.Auth;

public sealed class BasicAuthenticationHandlerTests
{
	private const string SchemeName = "Submarine.Basic";
	private readonly IUserCredentialVerifier _verifier = Substitute.For<IUserCredentialVerifier>();

	private BasicAuthenticationHandler CreateHandler()
		=> new(
			new OptionsMonitorStub<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
			NullLoggerFactory.Instance,
			UrlEncoder.Default,
			_verifier);

	private async Task<AuthenticateResult> AuthenticateAsync(string? authorizationHeader)
	{
		var context = new DefaultHttpContext();
		if (authorizationHeader is not null)
		{
			context.Request.Headers.Authorization = authorizationHeader;
		}

		context.RequestServices = new ServiceCollection().BuildServiceProvider();

		var handler = CreateHandler();
		var scheme = new AuthenticationScheme(SchemeName, SchemeName, typeof(BasicAuthenticationHandler));
		await handler.InitializeAsync(scheme, context);
		return await handler.AuthenticateAsync();
	}

	private static string BasicHeader(string username, string password)
		=> "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldSucceed_WithValidCredentials()
	{
		var user = new User { Id = 1, Username = "admin" };
		_verifier.VerifyAsync("admin", "correct-horse", Arg.Any<CancellationToken>()).Returns(user);

		var result = await AuthenticateAsync(BasicHeader("admin", "correct-horse"));

		result.Succeeded.ShouldBeTrue();
		result.Principal!.FindFirstValue(ClaimTypes.Name).ShouldBe("admin");
	}

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldFail_WithWrongPassword()
	{
		_verifier.VerifyAsync("admin", "wrong", Arg.Any<CancellationToken>()).Returns((User?)null);

		var result = await AuthenticateAsync(BasicHeader("admin", "wrong"));

		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldFail_WhenHeaderMissing()
		=> (await AuthenticateAsync(null)).Succeeded.ShouldBeFalse();

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldFail_WhenHeaderIsNotBasic()
		=> (await AuthenticateAsync("Bearer sometoken")).Succeeded.ShouldBeFalse();

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldFail_WhenHeaderIsMalformedBase64()
		=> (await AuthenticateAsync("Basic not-base64!")).Succeeded.ShouldBeFalse();

	[Fact]
	public async Task HandleChallengeAsync_ShouldSendWwwAuthenticateHeader()
	{
		var context = new DefaultHttpContext();
		context.RequestServices = new ServiceCollection().BuildServiceProvider();
		var handler = CreateHandler();
		var scheme = new AuthenticationScheme(SchemeName, SchemeName, typeof(BasicAuthenticationHandler));
		await handler.InitializeAsync(scheme, context);

		await handler.ChallengeAsync(null);

		context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
		context.Response.Headers.WWWAuthenticate.ToString().ShouldStartWith("Basic realm=");
	}

	private sealed class OptionsMonitorStub<T>(T value) : IOptionsMonitor<T>
	{
		public T CurrentValue => value;

		public T Get(string? name) => value;

		public IDisposable? OnChange(Action<T, string?> listener) => null;
	}
}
