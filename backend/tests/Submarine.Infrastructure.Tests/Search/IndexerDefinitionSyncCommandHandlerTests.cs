using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class IndexerDefinitionSyncCommandHandlerTests
{
	[Fact]
	public async Task ExecuteAsync_ShouldDownloadDefinitionUpsertDatabaseAndReportProgress()
	{
		var appDataPath = Path.Combine(Path.GetTempPath(), $"submarine-definitions-{Guid.NewGuid():N}");
		Directory.CreateDirectory(appDataPath);
		using var db = TestDbFactory.Create(TimeProvider.System);
		using var http = new HttpClient(new DefinitionHandler());
		var loader = new IndexerDefinitionLoader(
			NullLogger<IndexerDefinitionLoader>.Instance,
			appDataPath,
			Submarine.Infrastructure.Tests.Indexers.BundledDefinitionsTests.FindDefinitionsFolder());
		var syncClient = new IndexerDefinitionSyncClient(http, loader, NullLogger<IndexerDefinitionSyncClient>.Instance);
		var context = Substitute.For<ICommandContext>();
		var handler = new IndexerDefinitionSyncCommandHandler(syncClient, loader, db, TimeProvider.System);

		try
		{
			await handler.ExecuteAsync(new IndexerDefinitionSyncCommand(), context, TestContext.Current.CancellationToken);

			File.Exists(Path.Combine(appDataPath, "yts.yml")).ShouldBeTrue();
			var saved = await db.IndexerDefinitions.SingleAsync(row => row.DefinitionId == "yts", TestContext.Current.CancellationToken);
			saved.Name.ShouldBe("YTS");
			saved.Protocol.ShouldBe(Protocol.BITTORRENT);
			saved.Type.ShouldBe(IndexerDefinitionType.PUBLIC);
			saved.Version.ShouldBe("1");
			saved.Yaml.ShouldContain("name: YTS");
			await context.Received(1).ReportProgressAsync(50, "Synced 1 definitions", Arg.Any<CancellationToken>());
			await context.Received(1).ReportProgressAsync(100, cancellationToken: Arg.Any<CancellationToken>());
		}
		finally
		{
			Directory.Delete(appDataPath, true);
		}
	}

	private sealed class DefinitionHandler : HttpMessageHandler
	{
		private const string Index = "[{\"id\":\"yts\",\"file\":\"yts.yml\",\"name\":\"YTS\",\"description\":\"YTS catalog\",\"language\":\"en-US\",\"type\":\"public\",\"protocol\":\"torrent\",\"links\":[\"https://yts.example/\"]}]";
		private readonly string _yaml = File.ReadAllText(Path.Combine(Submarine.Infrastructure.Tests.Indexers.BundledDefinitionsTests.FindDefinitionsFolder(), "yts.yml"));

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var content = request.RequestUri!.AbsolutePath.EndsWith("/master/11", StringComparison.Ordinal)
				? Index
				: _yaml;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(content)
			});
		}
	}
}
