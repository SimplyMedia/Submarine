using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Events;
using Submarine.Api.Jobs;
using Submarine.Api.Services;
using Xunit;

namespace Submarine.Api.Tests;

public class HealthCheckJobTest : DatabaseTestBase
{
	[Fact]
	public async Task ExecuteAsync_ShouldPublishOnlyNewIssues_WhenRunTwice()
	{
		var publisher = new FakeEventPublisher();
		var provider = BuildProvider(publisher);
		var job = new HealthCheckJob();

		await job.ExecuteAsync(provider, TestContext.Current.CancellationToken);

		Assert.NotEmpty(publisher.Published);
		Assert.All(publisher.Published, e => Assert.IsType<HealthIssueEvent>(e));

		publisher.Published.Clear();

		await job.ExecuteAsync(provider, TestContext.Current.CancellationToken);

		Assert.Empty(publisher.Published);
	}

	private IServiceProvider BuildProvider(FakeEventPublisher publisher)
	{
		var services = new ServiceCollection();

		services.AddSingleton(new HealthService(Context, Settings(), new FakeMetadataClient(),
			new FakeMappingsClient(), NullLogger<HealthService>.Instance));
		services.AddSingleton<IEventPublisher>(publisher);
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		return services.BuildServiceProvider();
	}
}
