using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Health;

/// <summary>
///     Asserts the API key validation and system time health checks.
/// </summary>
public sealed class SystemHealthChecksTests : IAsyncLifetime
{
	private SqliteConnection _connection = null!;
	private ServiceProvider _provider = null!;

	public async ValueTask InitializeAsync()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FakeTimeProvider());
		services.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(_connection));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		_provider = services.BuildServiceProvider();
		await Db.Database.EnsureCreatedAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	private SubmarineDbContext Db => _provider.GetRequiredService<SubmarineDbContext>();

	[Fact]
	public async Task ApiKeyCheck_ShouldWarn_WhenKeyIsShort()
	{
		Db.GeneralConfig.Add(new GeneralConfig { ApiKey = "short" });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new ApiKeyValidationHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Message.Contains("API key"));
	}

	[Fact]
	public async Task ApiKeyCheck_ShouldBeEmpty_WhenKeyIsLongEnough()
	{
		Db.GeneralConfig.Add(new GeneralConfig { ApiKey = new string('a', 32) });
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new ApiKeyValidationHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task TrustedProxyAuthenticationCheck_ShouldWarn_WhenLocalAuthHasNoTrustedProxies()
	{
		Db.GeneralConfig.Add(new GeneralConfig
		{
			AuthenticationRequired = AuthenticationRequiredType.DISABLED_FOR_LOCAL_ADDRESSES,
			TrustedProxies = string.Empty
		});
		await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var issues = await new TrustedProxyAuthenticationHealthCheck(Db).CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.WARNING && x.Source == "Authentication");
	}

	[Fact]
	public async Task SystemTimeCheck_ShouldError_WhenClockIsOffByMoreThanADay()
	{
		var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
		var factory = Substitute.For<IHttpClientFactory>();
		factory.CreateClient("health").Returns(_ => new HttpClient(new DateHeaderHandler(DateTimeOffset.UtcNow.AddDays(3))));

		var issues = await new SystemTimeHealthCheck(factory, timeProvider, NullLogger<SystemTimeHealthCheck>.Instance)
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldContain(x => x.Type == HealthIssueType.ERROR && x.Message.Contains("clock"));
	}

	[Fact]
	public async Task SystemTimeCheck_ShouldBeEmpty_WhenClockMatches()
	{
		var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
		var factory = Substitute.For<IHttpClientFactory>();
		factory.CreateClient("health").Returns(_ => new HttpClient(new DateHeaderHandler(DateTimeOffset.UtcNow)));

		var issues = await new SystemTimeHealthCheck(factory, timeProvider, NullLogger<SystemTimeHealthCheck>.Instance)
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task SystemTimeCheck_ShouldBeEmpty_WhenRemoteIsUnreachable()
	{
		var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
		var factory = Substitute.For<IHttpClientFactory>();
		factory.CreateClient("health").Returns(_ => new HttpClient(new ThrowingHandler()));

		var issues = await new SystemTimeHealthCheck(factory, timeProvider, NullLogger<SystemTimeHealthCheck>.Instance)
			.CheckAsync(TestContext.Current.CancellationToken);

		issues.ShouldBeEmpty();
	}

	private sealed class DateHeaderHandler(DateTimeOffset date) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK);
			response.Headers.Date = date;
			return Task.FromResult(response);
		}
	}

	private sealed class ThrowingHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> throw new HttpRequestException("unreachable");
	}
}
