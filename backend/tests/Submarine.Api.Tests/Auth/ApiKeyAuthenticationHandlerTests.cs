using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Submarine.Api.Auth;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Auth;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit;

namespace Submarine.Api.Tests.Auth;

public sealed class ApiKeyAuthenticationHandlerTests
{
	private const string SchemeName = "ApiKey";
	private readonly IAuthConfigProvider _authConfigProvider = Substitute.For<IAuthConfigProvider>();

	private ApiKeyAuthenticationHandler CreateHandler(HttpContext context)
		=> new(
			new OptionsMonitorStub<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
			NullLoggerFactory.Instance,
			UrlEncoder.Default,
			_authConfigProvider);

	private async Task<AuthenticateResult> AuthenticateAsync(string? headerValue, string? queryValue = null)
	{
		var context = new DefaultHttpContext();
		if (headerValue is not null)
		{
			context.Request.Headers["X-Api-Key"] = headerValue;
		}

		if (queryValue is not null)
		{
			context.Request.QueryString = new QueryString($"?apikey={queryValue}");
		}

		context.RequestServices = new ServiceCollection()
			.AddSingleton(_authConfigProvider)
			.BuildServiceProvider();

		var handler = CreateHandler(context);
		var scheme = new AuthenticationScheme(SchemeName, SchemeName, typeof(ApiKeyAuthenticationHandler));
		await handler.InitializeAsync(scheme, context);
		return await handler.AuthenticateAsync();
	}

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldSucceed_WithValidHeaderKey()
	{
		_authConfigProvider.GetSnapshotAsync(Arg.Any<CancellationToken>())
			.Returns(new AuthSnapshot(AuthMethod.FORMS, "0123456789abcdef", string.Empty, AuthenticationRequiredType.ENABLED, []));

		var result = await AuthenticateAsync("0123456789abcdef");

		result.Succeeded.ShouldBeTrue();
		result.Principal!.Identity!.IsAuthenticated.ShouldBeTrue();
		result.Principal.FindFirstValue(ClaimTypes.Name).ShouldBe("api-key");
	}

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldSucceed_WithValidQueryKey()
	{
		_authConfigProvider.GetSnapshotAsync(Arg.Any<CancellationToken>())
			.Returns(new AuthSnapshot(AuthMethod.FORMS, "0123456789abcdef", string.Empty, AuthenticationRequiredType.ENABLED, []));

		var result = await AuthenticateAsync(null, "0123456789abcdef");

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldFail_WithWrongKey()
	{
		_authConfigProvider.GetSnapshotAsync(Arg.Any<CancellationToken>())
			.Returns(new AuthSnapshot(AuthMethod.FORMS, "0123456789abcdef", string.Empty, AuthenticationRequiredType.ENABLED, []));

		var result = await AuthenticateAsync("deadbeef");

		result.Succeeded.ShouldBeFalse();
		result.Failure.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldFail_WhenNoKeyConfigured()
	{
		_authConfigProvider.GetSnapshotAsync(Arg.Any<CancellationToken>())
			.Returns(new AuthSnapshot(AuthMethod.FORMS, string.Empty, string.Empty, AuthenticationRequiredType.ENABLED, []));

		var result = await AuthenticateAsync("anything");

		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WhenNoKeyProvided()
		=> (await AuthenticateAsync(null)).None.ShouldBeTrue();

	private sealed class OptionsMonitorStub<T>(T value) : IOptionsMonitor<T>
	{
		public T CurrentValue => value;

		public T Get(string? name) => value;

		public IDisposable? OnChange(Action<T, string?> listener) => null;
	}
}
