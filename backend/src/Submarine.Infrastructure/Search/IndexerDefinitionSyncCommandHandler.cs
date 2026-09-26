using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Syncs Cardigann definitions from the upstream repository to the app data folder, then upserts an
///     <see cref="IndexerDefinition" /> row per synced definition so listings and versioning survive without hitting
///     the disk.
/// </summary>
public sealed class IndexerDefinitionSyncCommandHandler(
	IndexerDefinitionSyncClient syncClient,
	IndexerDefinitionLoader loader,
	SubmarineDbContext db,
	TimeProvider timeProvider) : ICommandHandler<IndexerDefinitionSyncCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(IndexerDefinitionSyncCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var count = await syncClient.SyncAsync(cancellationToken);
		await context.ReportProgressAsync(50, $"Synced {count} definitions", cancellationToken);

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var files = Directory.Exists(loader.AppDataPath)
			? Directory.EnumerateFiles(loader.AppDataPath, "*.yml", SearchOption.TopDirectoryOnly)
			: [];

		foreach (var path in files)
		{
			var definitionId = Path.GetFileNameWithoutExtension(path);
			var definition = loader.Load(definitionId);
			if (definition is null)
			{
				continue;
			}

			var row = await db.IndexerDefinitions.FirstOrDefaultAsync(candidate => candidate.DefinitionId == definitionId, cancellationToken);
			if (row is null)
			{
				row = new IndexerDefinition { DefinitionId = definitionId };
				db.IndexerDefinitions.Add(row);
			}

			row.Name = definition.Name;
			row.Description = definition.Description;
			row.Language = definition.Language;
			row.Type = ParseType(definition.Type);
			row.Protocol = definition.ProtocolKind;
			row.Links = [.. definition.Links];
			row.Yaml = await File.ReadAllTextAsync(path, cancellationToken);
			row.Version = "1";
			row.UpstreamUpdatedAt = now;
		}

		await db.SaveChangesAsync(cancellationToken);
		await context.ReportProgressAsync(100, cancellationToken: cancellationToken);
	}

	private static IndexerDefinitionType ParseType(string type)
		=> type.ToLowerInvariant() switch
		{
			"private" => IndexerDefinitionType.PRIVATE,
			"semi-private" => IndexerDefinitionType.SEMI_PRIVATE,
			_ => IndexerDefinitionType.PUBLIC
		};
}
