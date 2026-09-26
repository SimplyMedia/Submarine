using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Submarine.Core.Download;

namespace Submarine.Api.IntegrationTests.Releases;

/// <summary>
///     Hosts the real API against a temporary Sqlite database with a substitutable <see cref="IDownloadClientFactory" />
///     so a release grab can be exercised without reaching a real download client.
/// </summary>
public sealed class ReleasesApiFactory : SubmarineApiFactory
{
	public IDownloadClientFactory DownloadClientFactory { get; } = Substitute.For<IDownloadClientFactory>();

	/// <inheritdoc />
	protected override void ConfigureTestServices(IServiceCollection services)
	{
		services.RemoveAll<IDownloadClientFactory>();
		services.AddSingleton(DownloadClientFactory);
	}
}
