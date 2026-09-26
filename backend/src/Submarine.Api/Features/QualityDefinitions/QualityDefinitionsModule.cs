using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Profiles;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.QualityDefinitions;

/// <summary>
///     Read and bulk update quality size definitions.
/// </summary>
public sealed class QualityDefinitionsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/quality-definitions");
		group.MapGet("/", ListAsync);
		group.MapPost("/import", ImportAsync);
		group.MapPut("/", UpdateAsync);
		group.MapPut("/{id:int}", UpdateOneAsync);
	}

	private static async Task<Ok<QualityDefinitionImportResult>> ImportAsync(
		SubmarineDbContext db,
		JsonDocument body,
		CancellationToken cancellationToken)
	{
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		List<TrashQualityDefinition> entries;

		if (body.RootElement.ValueKind is JsonValueKind.Array)
		{
			entries = body.Deserialize<List<TrashQualityDefinition>>(options) ?? [];
		}
		else if (body.RootElement.ValueKind is JsonValueKind.Object && body.RootElement.TryGetProperty("qualities", out var qualitiesElement))
		{
			entries = qualitiesElement.Deserialize<List<TrashQualityDefinition>>(options) ?? [];
		}
		else
		{
			throw new FluentValidation.ValidationException("Expected a TRaSH quality-size document or an array of quality entries");
		}

		var definitions = await db.QualityDefinitions.ToListAsync(cancellationToken);
		var updated = new List<QualityDefinitionResource>();
		var skipped = new List<string>();

		foreach (var entry in entries)
		{
			var resolved = TrashQualityDefinitionJson.Resolve(entry);
			var definition = resolved is { } match
				? definitions.FirstOrDefault(candidate => candidate.Source == match.Source && candidate.Resolution == match.Resolution)
				: null;

			if (resolved is not { } resolvedMatch || definition is null)
			{
				skipped.Add(entry.Quality);
				continue;
			}

			definition.MinSizeMbPerMinute = resolvedMatch.Min;
			definition.MaxSizeMbPerMinute = resolvedMatch.Max;
			definition.PreferredSizeMbPerMinute = resolvedMatch.Preferred;
			updated.Add(new QualityDefinitionResource(
				definition.Id,
				definition.Source?.ToString(),
				definition.Resolution?.ToString(),
				definition.Title,
				definition.MinSizeMbPerMinute,
				definition.MaxSizeMbPerMinute,
				definition.PreferredSizeMbPerMinute));
		}

		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(new QualityDefinitionImportResult(updated, skipped));
	}

	private static async Task<Ok<List<QualityDefinitionResource>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var definitions = await db.QualityDefinitions
			.OrderBy(definition => definition.Id)
			.Select(definition => new QualityDefinitionResource(
				definition.Id,
				definition.Source != null ? definition.Source.ToString() : null,
				definition.Resolution != null ? definition.Resolution.ToString() : null,
				definition.Title,
				definition.MinSizeMbPerMinute,
				definition.MaxSizeMbPerMinute,
				definition.PreferredSizeMbPerMinute))
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(definitions);
	}

	private static async Task<NoContent> UpdateAsync(
		SubmarineDbContext db,
		IReadOnlyList<QualityDefinitionUpdate> updates,
		CancellationToken cancellationToken)
	{
		foreach (var update in updates)
		{
			await ApplyAsync(db, update, cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static async Task<NoContent> UpdateOneAsync(
		int id,
		SubmarineDbContext db,
		QualityDefinitionUpdate update,
		CancellationToken cancellationToken)
	{
		if (update.Id != id)
		{
			throw new FluentValidation.ValidationException("The id in the path and the body must match");
		}

		await ApplyAsync(db, update, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.NoContent();
	}

	private static async Task ApplyAsync(
		SubmarineDbContext db,
		QualityDefinitionUpdate update,
		CancellationToken cancellationToken)
	{
		var definition = await db.QualityDefinitions.FirstOrDefaultAsync(entry => entry.Id == update.Id, cancellationToken)
			?? throw new KeyNotFoundException($"Quality definition {update.Id} does not exist");

		if (update.MinSizeMbPerMinute is { } min && update.MaxSizeMbPerMinute is { } max && min > max)
		{
			throw new FluentValidation.ValidationException($"Quality definition {update.Id}: min size must not exceed max size");
		}

		definition.MinSizeMbPerMinute = update.MinSizeMbPerMinute;
		definition.MaxSizeMbPerMinute = update.MaxSizeMbPerMinute;
		definition.PreferredSizeMbPerMinute = update.PreferredSizeMbPerMinute;
	}
}

/// <summary>A quality size definition.</summary>
/// <param name="Id">Id.</param>
/// <param name="Source">Quality source member name, null for unknown.</param>
/// <param name="Resolution">Resolution member name, null for unknown.</param>
/// <param name="Title">Display title.</param>
/// <param name="MinSizeMbPerMinute">Minimum size in MB per minute, null for unlimited.</param>
/// <param name="MaxSizeMbPerMinute">Maximum size in MB per minute, null for unlimited.</param>
/// <param name="PreferredSizeMbPerMinute">Preferred size in MB per minute.</param>
public sealed record QualityDefinitionResource(
	int Id,
	string? Source,
	string? Resolution,
	string Title,
	double? MinSizeMbPerMinute,
	double? MaxSizeMbPerMinute,
	double? PreferredSizeMbPerMinute);

/// <summary>Update of one quality definition's size limits.</summary>
/// <param name="Id">Id of the definition.</param>
/// <param name="MinSizeMbPerMinute">Minimum size in MB per minute, null for unlimited.</param>
/// <param name="MaxSizeMbPerMinute">Maximum size in MB per minute, null for unlimited.</param>
/// <param name="PreferredSizeMbPerMinute">Preferred size in MB per minute.</param>
public sealed record QualityDefinitionUpdate(
	int Id,
	double? MinSizeMbPerMinute,
	double? MaxSizeMbPerMinute,
	double? PreferredSizeMbPerMinute);

/// <summary>Result of importing TRaSH quality-size entries.</summary>
/// <param name="Updated">Definitions updated by the import.</param>
/// <param name="Skipped">TRaSH quality names that did not match a known Submarine quality.</param>
public sealed record QualityDefinitionImportResult(
	IReadOnlyList<QualityDefinitionResource> Updated,
	IReadOnlyList<string> Skipped);
