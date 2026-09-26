using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Submarine.Infrastructure.Updates;
using Xunit;

namespace Submarine.Api.Tests.System;

/// <summary>
///     Asserts version comparison and caching of the GitHub update checker.
/// </summary>
public sealed class UpdateCheckerTests
{
	private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
		: HttpMessageHandler
	{
		public int Calls { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;
			return Task.FromResult(responder(request));
		}
	}

	private static (GitHubUpdateChecker Checker, CountingHandler Handler) Create(string? tag)
	{
		var handler = new CountingHandler(_ => tag is null
			? new HttpResponseMessage(HttpStatusCode.NotFound)
			: new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent($$"""{"tag_name":"{{tag}}"}""", Encoding.UTF8, "application/json")
			});
		var services = new ServiceCollection();
		services.AddMemoryCache();
		services.AddHttpClient(GitHubUpdateChecker.HttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => handler);
		services.AddSingleton<IUpdateChecker>(sp => new GitHubUpdateChecker(
			sp.GetRequiredService<IHttpClientFactory>(),
			sp.GetRequiredService<IMemoryCache>(),
			NullLogger<GitHubUpdateChecker>.Instance));
		var provider = services.BuildServiceProvider();
		return ((GitHubUpdateChecker)provider.GetRequiredService<IUpdateChecker>(), handler);
	}

	[Theory]
	[InlineData("v1.2.3", "1.2.3")]
	[InlineData("1.2.3+build.7", "1.2.3")]
	[InlineData("1.2.3-rc.1", "1.2.3")]
	[InlineData("2.10", "2.10")]
	public void TryNormalize_ShouldStripPrefixesAndSuffixes(string input, string expected)
	{
		GitHubUpdateChecker.TryNormalize(input, out var normalized).ShouldBeTrue();
		normalized.ToString().ShouldBe(expected);
	}

	[Fact]
	public async Task GetLatest_ShouldReportUpdateAvailable_WhenNewerTagExists()
	{
		var (checker, _) = Create("v99.0.0");

		var info = await checker.GetLatestAsync(cancellationToken: TestContext.Current.CancellationToken);

		info.UpdateAvailable.ShouldBeTrue();
		info.LatestVersion.ShouldBe("v99.0.0");
		info.ReleaseNotesUrl.ShouldNotBeNull();
		info.CurrentVersion.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task GetLatest_ShouldReportNoUpdate_WhenTagOlderOrUnknown()
	{
		var (older, _) = Create("v0.0.1");
		(await older.GetLatestAsync(cancellationToken: TestContext.Current.CancellationToken))
			.UpdateAvailable.ShouldBeFalse();

		var unknown = Create(null).Checker;
		var info = await unknown.GetLatestAsync(cancellationToken: TestContext.Current.CancellationToken);
		info.UpdateAvailable.ShouldBeFalse();
		info.LatestVersion.ShouldBeNull();
		info.ReleaseNotesUrl.ShouldBeNull();
	}

	[Fact]
	public async Task GetLatest_ShouldCacheForSixHours()
	{
		var (checker, handler) = Create("v2.0.0");

		await checker.GetLatestAsync(cancellationToken: TestContext.Current.CancellationToken);
		await checker.GetLatestAsync(cancellationToken: TestContext.Current.CancellationToken);
		handler.Calls.ShouldBe(1);

		await checker.GetLatestAsync(bypassCache: true, TestContext.Current.CancellationToken);
		handler.Calls.ShouldBe(2);
	}
}
