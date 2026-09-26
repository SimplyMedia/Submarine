using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Indexers.Cardigann;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.IndexerDefinitions;

/// <summary>
///     Lists bundled and synced Cardigann definitions and triggers a sync from the upstream repository.
/// </summary>
public sealed class IndexerDefinitionsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/indexer-definitions");
		group.MapGet("/", ListAsync);
		group.MapGet("/{definitionId}", GetAsync);
		group.MapPost("/sync", SyncAsync);
	}

	private static async Task<Ok<IReadOnlyList<IndexerDefinitionDto>>> ListAsync(
		IndexerDefinitionLoader loader,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var installedIds = await db.Indexers.AsNoTracking()
			.Where(indexer => indexer.DefinitionId != null)
			.Select(indexer => indexer.DefinitionId!)
			.Distinct()
			.ToListAsync(cancellationToken);
		var installed = installedIds.ToHashSet();

		var definitions = loader.LoadAll()
			.Select(definition => ToDto(definition.ToInfo(), installed.Contains(definition.Id)))
			.ToList();

		return TypedResults.Ok((IReadOnlyList<IndexerDefinitionDto>)definitions);
	}

	private static async Task<Results<Ok<IndexerDefinitionDto>, NotFound>> GetAsync(
		string definitionId,
		IndexerDefinitionLoader loader,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var definition = loader.Load(definitionId);
		if (definition is null)
		{
			return TypedResults.NotFound();
		}

		var installed = await db.Indexers.AsNoTracking().AnyAsync(indexer => indexer.DefinitionId == definitionId, cancellationToken);
		return TypedResults.Ok(ToDto(definition.ToInfo(), installed));
	}

	private static async Task<Created<Command>> SyncAsync(ICommandQueue queue, CancellationToken cancellationToken)
	{
		var command = await queue.EnqueueAsync(new IndexerDefinitionSyncCommand(), CommandTrigger.MANUAL, cancellationToken: cancellationToken);
		return TypedResults.Created($"/api/v1/commands/{command.Id}", command);
	}

	private static IndexerDefinitionDto ToDto(IndexerDefinitionInfo info, bool installed)
		=> new(info.Id, info.Name, info.Description, info.Language, info.Type, info.Protocol, info.Links, info.LegacyLinks, info.Settings, info.Categories, installed);
}

/// <summary>A Cardigann indexer definition available to configure, bundled or synced from the upstream repository.</summary>
public sealed record IndexerDefinitionDto(
	string Id,
	string Name,
	string Description,
	string Language,
	string Type,
	string Protocol,
	IReadOnlyList<string> Links,
	IReadOnlyList<string> LegacyLinks,
	IReadOnlyList<IndexerSettingField> Settings,
	IReadOnlyList<int> Categories,
	bool Installed);
