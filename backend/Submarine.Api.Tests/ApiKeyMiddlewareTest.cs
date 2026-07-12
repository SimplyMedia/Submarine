using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Middleware;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Xunit;

namespace Submarine.Api.Tests;

public class ApiKeyMiddlewareTest
{
	private const string Key = "test-api-key";

	private bool _nextCalled;

	private ApiKeyMiddleware CreateMiddleware(AuthenticationMethod method, string environment = "Production",
		Dictionary<string, string?>? settings = null, string? apiKey = Key)
	{
		var store = new SecurityConfigStore(null!);
		store.Set(new SecurityConfig { Id = 1, Method = method, ApiKey = apiKey! });

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
			.Build();

		return new ApiKeyMiddleware(_ =>
			{
				_nextCalled = true;
				return Task.CompletedTask;
			},
			store,
			configuration,
			new FakeHostEnvironment(environment),
			NullLogger<ApiKeyMiddleware>.Instance);
	}

	private static DefaultHttpContext CreateContext(string path)
	{
		var context = new DefaultHttpContext();
		context.Request.Path = path;

		return context;
	}

	[Fact]
	public async Task InvokeAsync_ShouldReturn401_WhenApiKeyIsMissing()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.API_KEY);
		var context = CreateContext("/api/v1/series");

		await middleware.InvokeAsync(context);

		Assert.False(_nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
	}

	[Fact]
	public async Task InvokeAsync_ShouldCallNext_WhenBearerTokenMatches()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.API_KEY);
		var context = CreateContext("/api/v1/series");
		context.Request.Headers.Authorization = $"Bearer {Key}";

		await middleware.InvokeAsync(context);

		Assert.True(_nextCalled);
	}

	[Fact]
	public async Task InvokeAsync_ShouldReturn401_WhenAuthorizationHeaderLacksBearerPrefix()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.API_KEY);
		var context = CreateContext("/api/v1/series");
		context.Request.Headers.Authorization = Key;

		await middleware.InvokeAsync(context);

		Assert.False(_nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
	}

	[Fact]
	public async Task InvokeAsync_ShouldCallNext_WhenMethodIsNone()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.NONE);
		var context = CreateContext("/api/v1/series");

		await middleware.InvokeAsync(context);

		Assert.True(_nextCalled);
	}

	[Fact]
	public async Task InvokeAsync_ShouldCallNext_WhenPathIsStatusEndpoint()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.API_KEY);
		var context = CreateContext("/_status/healthz");

		await middleware.InvokeAsync(context);

		Assert.True(_nextCalled);
	}

	[Fact]
	public async Task InvokeAsync_ShouldCallNext_WhenDevelopmentAndAuthMethodNotConfigured()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.API_KEY, "Development");
		var context = CreateContext("/api/v1/series");

		await middleware.InvokeAsync(context);

		Assert.True(_nextCalled);
	}

	[Fact]
	public async Task InvokeAsync_ShouldReturn401_WhenDevelopmentAndConfiguredMethodIsApiKey()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.API_KEY, "Development",
			new Dictionary<string, string?> { ["Auth:Method"] = "ApiKey" });
		var context = CreateContext("/api/v1/series");

		await middleware.InvokeAsync(context);

		Assert.False(_nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
	}

	[Fact]
	public async Task InvokeAsync_ShouldReturn401_WhenAuthMethodUnrecognized()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.NONE,
			settings: new Dictionary<string, string?> { ["Auth:Method"] = "typo" });
		var context = CreateContext("/api/v1/series");

		await middleware.InvokeAsync(context);

		Assert.False(_nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
	}

	[Fact]
	public async Task InvokeAsync_ShouldReturn401_WhenStoredApiKeyEmpty()
	{
		var middleware = CreateMiddleware(AuthenticationMethod.API_KEY, apiKey: "");
		var context = CreateContext("/api/v1/series");
		context.Request.Headers.Authorization = "Bearer ";

		await middleware.InvokeAsync(context);

		Assert.False(_nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
	}

	private sealed class FakeHostEnvironment : IHostEnvironment
	{
		public FakeHostEnvironment(string environmentName)
			=> EnvironmentName = environmentName;

		public string EnvironmentName { get; set; }

		public string ApplicationName { get; set; } = "Submarine.Api.Tests";

		public string ContentRootPath { get; set; } = string.Empty;

		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
