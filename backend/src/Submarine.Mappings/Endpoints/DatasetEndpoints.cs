using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Data;
using Submarine.Mappings.Entities;
using Submarine.Mappings.Infrastructure;
using Submarine.Mappings.Models;
using Submarine.Mappings.Services;

namespace Submarine.Mappings.Endpoints;

/// <summary>
/// Whole dataset export and idempotent import. Import upserts on natural keys and ignores row ids.
/// </summary>
public static class DatasetEndpoints
{
	public static IEndpointRouteBuilder MapDatasetEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapGet("/api/v1/export", async (MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var sceneMappings = await db.SceneMappings.OrderBy(m => m.Id).ToListAsync(cancellationToken);
			var episodeMappings = await db.SceneEpisodeMappings.OrderBy(m => m.Id).ToListAsync(cancellationToken);
			var aniListMappings = await db.AniListMappings.OrderBy(m => m.Id).ToListAsync(cancellationToken);
			var sceneNames = await db.SceneNames.OrderBy(n => n.Id).ToListAsync(cancellationToken);

			return Results.Ok(new MappingsExport(
				sceneMappings.Select(SceneEndpoints.ToResource).ToList(),
				episodeMappings.Select(SceneEndpoints.ToEpisodeResource).ToList(),
				aniListMappings.Select(AniListMappingService.ToResource).ToList(),
				sceneNames.Select(SceneEndpoints.ToNameResource).ToList()));
		});

		app.MapPost("/api/v1/import", async Task<IResult> (
			MappingsExport export,
			MappingsDbContext db,
			IValidator<CreateSceneMappingRequest> sceneMappingValidator,
			IValidator<CreateSceneEpisodeMappingRequest> episodeMappingValidator,
			IValidator<CreateAniListMappingRequest> aniListValidator,
			IValidator<CreateSceneNameRequest> sceneNameValidator,
			CancellationToken cancellationToken) =>
		{
			var failures = new List<string>();
			foreach (var mapping in export.SceneMappings)
			{
				if (!(await sceneMappingValidator.ValidateAsync(ToCreate(mapping), cancellationToken)).IsValid)
				{
					failures.Add($"sceneMapping '{mapping.Title}' (tvdbId {mapping.TvdbId})");
				}
			}

			foreach (var mapping in export.EpisodeMappings)
			{
				if (!(await episodeMappingValidator.ValidateAsync(ToCreate(mapping), cancellationToken)).IsValid)
				{
					failures.Add($"episodeMapping (tvdbId {mapping.TvdbId}, s{mapping.SeasonNumber}e{mapping.EpisodeNumber})");
				}
			}

			foreach (var mapping in export.AniListMappings)
			{
				if (!(await aniListValidator.ValidateAsync(ToCreate(mapping), cancellationToken)).IsValid)
				{
					failures.Add($"aniListMapping '{mapping.Title}' (aniListId {mapping.AniListId})");
				}
			}

			foreach (var name in export.SceneNames)
			{
				if (!(await sceneNameValidator.ValidateAsync(ToCreate(name), cancellationToken)).IsValid)
				{
					failures.Add($"sceneName '{name.SceneName}' (tvdbId {name.TvdbId})");
				}
			}

			if (failures.Count > 0)
			{
				return Results.ValidationProblem(new Dictionary<string, string[]>
				{
					["export"] = [$"Invalid items: {string.Join(", ", failures)}"],
				});
			}

			var existingMappings = await db.SceneMappings.ToListAsync(cancellationToken);
			var existingEpisodeMappings = await db.SceneEpisodeMappings.ToListAsync(cancellationToken);
			var existingAniListMappings = await db.AniListMappings.ToListAsync(cancellationToken);
			var existingNames = await db.SceneNames.ToListAsync(cancellationToken);

			foreach (var resource in export.SceneMappings)
			{
				var entity = existingMappings.FirstOrDefault(m => m.TvdbId == resource.TvdbId && m.SeasonNumber == resource.SeasonNumber);
				if (entity is null)
				{
					entity = new SceneMapping { TvdbId = resource.TvdbId, SeasonNumber = resource.SeasonNumber };
					db.SceneMappings.Add(entity);
					existingMappings.Add(entity);
				}

				entity.Title = resource.Title;
				entity.SceneSeasonNumber = resource.SceneSeasonNumber;
				entity.EpisodeOffset = resource.EpisodeOffset;
				entity.SearchTitle = resource.SearchTitle;
				entity.Comment = resource.Comment;
			}

			foreach (var resource in export.EpisodeMappings)
			{
				var entity = existingEpisodeMappings.FirstOrDefault(m =>
					m.TvdbId == resource.TvdbId && m.SeasonNumber == resource.SeasonNumber && m.EpisodeNumber == resource.EpisodeNumber);
				if (entity is null)
				{
					entity = new SceneEpisodeMapping { TvdbId = resource.TvdbId, SeasonNumber = resource.SeasonNumber, EpisodeNumber = resource.EpisodeNumber };
					db.SceneEpisodeMappings.Add(entity);
					existingEpisodeMappings.Add(entity);
				}

				entity.SceneSeasonNumber = resource.SceneSeasonNumber;
				entity.SceneEpisodeNumber = resource.SceneEpisodeNumber;
			}

			foreach (var resource in export.AniListMappings)
			{
				var entity = existingAniListMappings.FirstOrDefault(m => m.AniListId == resource.AniListId);
				if (entity is null)
				{
					entity = new AniListMapping { AniListId = resource.AniListId };
					db.AniListMappings.Add(entity);
					existingAniListMappings.Add(entity);
				}

				entity.TvdbId = resource.TvdbId;
				entity.Title = resource.Title;
				entity.TvdbSeason = resource.TvdbSeason;
				entity.EpisodeStart = resource.EpisodeStart;
				entity.EpisodeCount = resource.EpisodeCount;
				entity.AbsoluteOffset = resource.AbsoluteOffset;
			}

			foreach (var resource in export.SceneNames)
			{
				var entity = existingNames.FirstOrDefault(n => n.TvdbId == resource.TvdbId && n.SceneName == resource.SceneName);
				if (entity is null)
				{
					entity = new SceneNameEntry { TvdbId = resource.TvdbId, SceneName = resource.SceneName };
					db.SceneNames.Add(entity);
					existingNames.Add(entity);
				}

				entity.SeasonNumber = resource.SeasonNumber;
			}

			await db.SaveChangesAsync(cancellationToken);

			return Results.Ok(new ImportResult(
				export.SceneMappings.Count,
				export.EpisodeMappings.Count,
				export.AniListMappings.Count,
				export.SceneNames.Count));
		}).AddEndpointFilter<AdminApiKeyFilter>();

		return app;
	}

	public static CreateSceneMappingRequest ToCreate(SceneMappingResource resource) => new(
		resource.TvdbId,
		resource.Title,
		resource.SeasonNumber,
		resource.SceneSeasonNumber,
		resource.EpisodeOffset,
		resource.SearchTitle,
		resource.Comment);

	public static CreateSceneEpisodeMappingRequest ToCreate(SceneEpisodeMappingResource resource) => new(
		resource.TvdbId,
		resource.SeasonNumber,
		resource.EpisodeNumber,
		resource.SceneSeasonNumber,
		resource.SceneEpisodeNumber);

	public static CreateAniListMappingRequest ToCreate(AniListMappingResource resource) => new(
		resource.AniListId,
		resource.TvdbId,
		resource.Title,
		resource.TvdbSeason,
		resource.EpisodeStart,
		resource.EpisodeCount,
		resource.AbsoluteOffset);

	public static CreateSceneNameRequest ToCreate(SceneNameResource resource) => new(
		resource.TvdbId,
		resource.SceneName,
		resource.SeasonNumber);
}
