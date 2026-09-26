using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Submarine.Api.IntegrationTests.Library;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.MediaFiles;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.IntegrationTests.Downloads;

/// <summary>
///     Hosts the real API against a temporary Sqlite database with substitutable IMetadataClient,
///     IMediaInfoService and IDownloadClientProvider, so the download/import stack can be exercised without
///     reaching real external services.
/// </summary>
public sealed class DownloadsApiFactory : SubmarineApiFactory
{
	public StubMetadataClient Metadata { get; } = new();

	/// <summary>Stubbed media info, returns null (no probe) unless a test overrides <see cref="ProbeResult" />.</summary>
	public MediaInfoModel? ProbeResult { get; set; }

	/// <summary>Stubbed download client provider, empty until a test populates it via <see cref="FakeDownloadClientProvider.Set" />.</summary>
	public FakeDownloadClientProvider ClientProvider { get; } = new();

	/// <inheritdoc />
	protected override void ConfigureTestServices(IServiceCollection services)
	{
		services.RemoveAll<IMetadataClient>();
		services.AddSingleton<IMetadataClient>(Metadata);

		services.RemoveAll<IMediaInfoService>();
		services.AddSingleton<IMediaInfoService>(new StubMediaInfoService(this));

		services.RemoveAll<IDownloadClientProvider>();
		services.AddSingleton<IDownloadClientProvider>(ClientProvider);
	}

	/// <summary>Create a client after migrating the database.</summary>

	private sealed class StubMediaInfoService(DownloadsApiFactory factory) : IMediaInfoService
	{
		public Task<MediaInfoModel?> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
			=> Task.FromResult(factory.ProbeResult);
	}
}

/// <summary>
///     In-memory <see cref="IDownloadClientProvider" /> a test populates directly with fake clients.
/// </summary>
public sealed class FakeDownloadClientProvider : IDownloadClientProvider
{
	private readonly List<EnabledDownloadClient> _clients = [];

	/// <summary>Replace the enabled clients returned by the provider.</summary>
	public void Set(params EnabledDownloadClient[] clients)
	{
		_clients.Clear();
		_clients.AddRange(clients);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<EnabledDownloadClient>> GetEnabledAsync(Submarine.Core.Provider.Protocol? protocol = null, CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<EnabledDownloadClient>>([.. _clients.Where(x => protocol is null || x.Instance.Protocol == protocol)]);

	/// <inheritdoc />
	public Task<EnabledDownloadClient?> GetAsync(int id, CancellationToken cancellationToken = default)
		=> Task.FromResult(_clients.FirstOrDefault(x => x.Entity.Id == id));
}
