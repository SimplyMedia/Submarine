using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Api.Events;
using Submarine.Api.Models.Database;
using Submarine.Api.Services;
using Submarine.Core.Download;
using Submarine.Core.Indexer;
using Submarine.Core.MediaFile.Naming;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Mappings.Contracts;
using Submarine.Metadata.Contracts;

namespace Submarine.Api.Tests;

/// <summary>
///     Creates a SQLite backed database context on a temporary file for a single test class
/// </summary>
public abstract class DatabaseTestBase : IDisposable
{
	private readonly string _dbPath;

	protected readonly SqliteDatabaseContext Context;

	protected DatabaseTestBase()
	{
		_dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:SqliteConnection"] = $"Data Source={_dbPath}"
			})
			.Build();

		Context = new SqliteDatabaseContext(new DbContextOptionsBuilder<SqliteDatabaseContext>().Options, configuration);
		Context.Database.EnsureCreated();
	}

	protected static MediaNamingService NamingService()
		=> new(new NamingTemplateRenderer());

	protected static IParser<BaseRelease> ReleaseParser()
		=> new ReleaseParserService(
			NullLogger<ReleaseParserService>.Instance,
			new LanguageParserService(NullLogger<LanguageParserService>.Instance),
			new StreamingProviderParserService(NullLogger<StreamingProviderParserService>.Instance),
			new QualityParserService(NullLogger<QualityParserService>.Instance),
			new ReleaseGroupParserService(NullLogger<ReleaseGroupParserService>.Instance));

	public void Dispose()
	{
		Context.Dispose();

		SqliteConnection.ClearAllPools();

		if (File.Exists(_dbPath))
			File.Delete(_dbPath);

		GC.SuppressFinalize(this);
	}
}

/// <summary>
///     Records published events instead of dispatching them
/// </summary>
public sealed class FakeEventPublisher : IEventPublisher
{
	public List<object> Published { get; } = new();

	public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
	{
		Published.Add(@event!);

		return ValueTask.CompletedTask;
	}
}

/// <summary>
///     Returns configured metadata without touching the network
/// </summary>
public sealed class FakeMetadataClient : IMetadataClient
{
	public SeriesResource? Series { get; set; }

	public MovieResource? Movie { get; set; }

	public Task<SeriesResource?> GetSeriesAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> Task.FromResult(Series);

	public Task<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> Task.FromResult(Movie);

	public Task<IReadOnlyList<SeriesResource>> SearchSeriesAsync(string term,
		CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<SeriesResource>>(Array.Empty<SeriesResource>());

	public Task<IReadOnlyList<MovieResource>> SearchMoviesAsync(string term,
		CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<MovieResource>>(Array.Empty<MovieResource>());
}

/// <summary>
///     Returns configured mappings without touching the network
/// </summary>
public sealed class FakeMappingsClient : IMappingsClient
{
	public SceneMappingSet? SceneMappings { get; set; }

	public IReadOnlyList<AniListMappingResource> AniListMappings { get; set; } =
		Array.Empty<AniListMappingResource>();

	public AniListResolution? AniListResolution { get; set; }

	public bool Unreachable { get; set; }

	public Task<SceneMappingSet?> GetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> Unreachable
			? throw new HttpRequestException("mappings unreachable")
			: Task.FromResult(SceneMappings);

	public Task<IReadOnlyList<AniListMappingResource>> GetAniListMappingsAsync(int tvdbId,
		CancellationToken cancellationToken = default)
		=> Unreachable
			? throw new HttpRequestException("mappings unreachable")
			: Task.FromResult(AniListMappings);

	public Task<AniListResolution?> ResolveAniListAsync(int tvdbId, int season, int episode,
		CancellationToken cancellationToken = default)
		=> Unreachable
			? throw new HttpRequestException("mappings unreachable")
			: Task.FromResult(AniListResolution);
}

/// <summary>
///     Records issued queries and returns a fixed set of releases without touching an indexer
/// </summary>
public sealed class FakeTorznabSearchClient : ITorznabSearchClient
{
	public List<(int? TvdbId, int? Season, int? Episode, string? Query)> TvSearches { get; } = new();

	public List<string?> TermSearches { get; } = new();

	public IReadOnlyList<ReleaseInfo> Result { get; set; } = Array.Empty<ReleaseInfo>();

	public Task<IReadOnlyList<ReleaseInfo>> TvSearchAsync(Provider indexer, int? tvdbId = null, int? season = null,
		int? episode = null, string? query = null, IReadOnlyList<int>? categories = null,
		CancellationToken cancellationToken = default)
	{
		TvSearches.Add((tvdbId, season, episode, query));

		return Task.FromResult(Result);
	}

	public Task<IReadOnlyList<ReleaseInfo>> MovieSearchAsync(Provider indexer, int? tmdbId = null, string? imdbId = null,
		string? query = null, IReadOnlyList<int>? categories = null, CancellationToken cancellationToken = default)
		=> Task.FromResult(Result);

	public Task<IReadOnlyList<ReleaseInfo>> SearchAsync(Provider indexer, string? query = null,
		IReadOnlyList<int>? categories = null, CancellationToken cancellationToken = default)
	{
		TermSearches.Add(query);

		return Task.FromResult(Result);
	}
}

/// <summary>
///     Captures grabbed releases and reports a fixed download id
/// </summary>
public sealed class FakeDownloadClient : IDownloadClient
{
	public Protocol Protocol { get; init; } = Protocol.BITTORRENT;

	public string DownloadId { get; init; } = "fake-download-id";

	public ReleaseInfo? LastAdded { get; private set; }

	public Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		LastAdded = release;

		return Task.FromResult(DownloadId);
	}

	public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<DownloadClientItem>>(Array.Empty<DownloadClientItem>());

	public Task RemoveItemAsync(string downloadId, bool deleteData, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	public Task TestAsync(CancellationToken cancellationToken = default)
		=> Task.CompletedTask;
}

/// <summary>
///     Hands out a fixed <see cref="FakeDownloadClient" /> instead of building a real client
/// </summary>
public sealed class FakeDownloadClientFactory : DownloadClientFactory
{
	private readonly IDownloadClient _client;

	public FakeDownloadClientFactory(IDownloadClient client) : base(null!, null!)
		=> _client = client;

	public override IDownloadClient Create(DownloadClientConfig config)
		=> _client;
}
