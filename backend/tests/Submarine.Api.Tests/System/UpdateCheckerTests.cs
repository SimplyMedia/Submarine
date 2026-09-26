using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Submarine.Infrastructure.Updates;
using Xunit;

namespace Submarine.Api.Tests.System;

/// <summary>
///     Asserts version comparison, branch selection and caching of the GitHub update checker.
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

	/// <summary>Fakes the GitHub releases list endpoint with zero or more releases, newest first.</summary>
	private static (GitHubUpdateChecker Checker, CountingHandler Handler) Create(params (string Tag, bool Prerelease)[] releases)
	{
		var body = "[" + string.Join(",", releases.Select(r =>
			$$"""{"tag_name":"{{r.Tag}}","prerelease":{{(r.Prerelease ? "true" : "false")}},"body":"notes for {{r.Tag}}","published_at":"2024-01-01T00:00:00Z"}""")) + "]";
		var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(body, Encoding.UTF8, "application/json")
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

	private static (GitHubUpdateChecker Checker, CountingHandler Handler) CreateFailing()
	{
		var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
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
	public async Task GetLatest_ShouldReportUpdateAvailable_WhenNewerStableReleaseExists()
	{
		var (checker, _) = Create(("v99.0.0", false));

		var info = await checker.GetLatestAsync("master", cancellationToken: TestContext.Current.CancellationToken);

		info.UpdateAvailable.ShouldBeTrue();
		info.LatestVersion.ShouldBe("v99.0.0");
		info.ReleaseNotesUrl.ShouldNotBeNull();
		info.CurrentVersion.ShouldNotBeNullOrEmpty();
		info.CheckFailed.ShouldBeFalse();
	}

	[Fact]
	public async Task GetLatest_ShouldReportNoUpdate_WhenTagOlder()
	{
		var (older, _) = Create(("v0.0.1", false));

		(await older.GetLatestAsync("master", cancellationToken: TestContext.Current.CancellationToken))
			.UpdateAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task GetLatest_ShouldReportNoLatestVersion_ButNotCheckFailed_WhenNoReleasesPublishedYet()
	{
		var (checker, _) = Create();

		var info = await checker.GetLatestAsync("master", cancellationToken: TestContext.Current.CancellationToken);

		info.UpdateAvailable.ShouldBeFalse();
		info.LatestVersion.ShouldBeNull();
		info.ReleaseNotesUrl.ShouldBeNull();
		info.CheckFailed.ShouldBeFalse();
	}

	[Fact]
	public async Task GetLatest_ShouldSetCheckFailed_WhenTheRequestFails()
	{
		var (checker, _) = CreateFailing();

		var info = await checker.GetLatestAsync("master", cancellationToken: TestContext.Current.CancellationToken);

		info.CheckFailed.ShouldBeTrue();
		info.LatestVersion.ShouldBeNull();
	}

	[Fact]
	public async Task GetLatest_ShouldSelectNewestStableRelease_ForNonDevelopBranch()
	{
		var (checker, _) = Create(("v2.0.0-develop", true), ("v1.5.0", false));

		var info = await checker.GetLatestAsync("master", cancellationToken: TestContext.Current.CancellationToken);

		info.LatestVersion.ShouldBe("v1.5.0");
	}

	[Fact]
	public async Task GetLatest_ShouldSelectNewestPrerelease_ForDevelopBranch()
	{
		var (checker, _) = Create(("v2.0.0-develop", true), ("v1.5.0", false));

		var info = await checker.GetLatestAsync("develop", cancellationToken: TestContext.Current.CancellationToken);

		info.LatestVersion.ShouldBe("v2.0.0-develop");
	}

	[Fact]
	public async Task GetReleases_ShouldMarkTheRunningVersionAsInstalled()
	{
		var current = GitHubUpdateChecker.CurrentVersion();
		var (checker, _) = Create((current, false), ("v0.0.1", false));

		var releases = await checker.GetReleasesAsync(cancellationToken: TestContext.Current.CancellationToken);

		releases.Single(r => r.Version == current).Installed.ShouldBeTrue();
		releases.Single(r => r.Version == "v0.0.1").Installed.ShouldBeFalse();
	}

	[Fact]
	public async Task GetLatest_ShouldCacheForSixHours()
	{
		var (checker, handler) = Create(("v2.0.0", false));

		await checker.GetLatestAsync("master", cancellationToken: TestContext.Current.CancellationToken);
		await checker.GetLatestAsync("master", cancellationToken: TestContext.Current.CancellationToken);
		handler.Calls.ShouldBe(1);

		await checker.GetLatestAsync("master", bypassCache: true, TestContext.Current.CancellationToken);
		handler.Calls.ShouldBe(2);
	}
}
