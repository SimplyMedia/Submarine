using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Submarine.Api.IntegrationTests.Library;
using Submarine.Core.Download;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Hosts the real API against a temporary Sqlite database with a substitutable
///     <see cref="IDownloadClientFactory" /> and a stubbed <see cref="IMetadataClient" />, so a Sonarr
///     compatibility release grab or series add can be exercised without reaching a real download client or the
///     metadata sibling service.
/// </summary>
public sealed class CompatSonarrExtendedApiFactory : SubmarineApiFactory
{
	public IDownloadClientFactory DownloadClientFactory { get; } = Substitute.For<IDownloadClientFactory>();

	public StubMetadataClient Metadata { get; } = new();

	/// <inheritdoc />
	protected override void ConfigureTestServices(IServiceCollection services)
	{
		services.RemoveAll<IDownloadClientFactory>();
		services.AddSingleton(DownloadClientFactory);
		services.RemoveAll<IMetadataClient>();
		services.AddSingleton<IMetadataClient>(Metadata);
	}
}
