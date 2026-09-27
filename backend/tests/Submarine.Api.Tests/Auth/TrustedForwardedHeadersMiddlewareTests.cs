using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Submarine.Api.Auth;
using Submarine.Core.Enums;
using Submarine.Core.Net;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Persistence;
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
	public async Task InvokeAsync_ShouldEmitHealthWarning_WhenLocalPeerForwardsWithoutTrustedProxies()
	{
		await using var connection = new SqliteConnection("DataSource=:memory:");
		await connection.OpenAsync();
		var provider = Substitute.For<IAuthConfigProvider>();
		provider.GetSnapshotAsync(Arg.Any<CancellationToken>())
			.Returns(new AuthSnapshot(AuthMethod.FORMS, "key", string.Empty,
				AuthenticationRequiredType.DISABLED_FOR_LOCAL_ADDRESSES, []));
		var logs = new CapturingLoggerProvider();
		var services = new ServiceCollection()
			.AddSingleton<TimeProvider>(TimeProvider.System)
			.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(connection))
			.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>())
			.AddMemoryCache()
			.AddSingleton<TrustedProxyWarningReporter>()
			.AddSingleton(provider)
			.AddLogging(builder => builder.AddProvider(logs))
			.BuildServiceProvider();
		await using var serviceProvider = services;
		var db = services.GetRequiredService<SubmarineDbContext>();
		await db.Database.EnsureCreatedAsync();
		var context = new DefaultHttpContext
		{
			RequestServices = services
		};
		context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
		context.Request.Headers["X-Forwarded-For"] = "203.0.113.9";

		await TrustedForwardedHeadersMiddleware.InvokeAsync(context, _ => Task.CompletedTask);

		logs.Messages.ShouldContain(message => message.Contains("TrustedProxies is empty"));
		context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("10.0.0.5"));
		var warning = await db.HealthIssues.SingleAsync();
		warning.Type.ShouldBe(HealthIssueType.WARNING);
		warning.Source.ShouldBe("Authentication");
		warning.Message.ShouldContain("Trusted proxies is empty");
	}

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		public List<string> Messages { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

		public void Dispose()
		{
		}
	}

	private sealed class CapturingLogger(List<string> messages) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (logLevel == LogLevel.Warning)
			{
				messages.Add(formatter(state, exception));
			}
		}
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
