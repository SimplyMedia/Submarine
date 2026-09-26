using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Data;
using Submarine.Mappings.Entities;
using Submarine.Mappings.Infrastructure;
using Submarine.Mappings.Services;

namespace Submarine.Mappings.Endpoints;

/// <summary>
/// Scene numbering endpoints: mapping sets, resolution, alternate scene titles and their CRUD.
/// </summary>
public static class SceneEndpoints
{
	public static IEndpointRouteBuilder MapSceneEndpoints(this IEndpointRouteBuilder app)
	{
		var scene = app.MapGroup("/api/v1/scene");

		scene.MapGet("/{tvdbId:int}", async Task<IResult> (int tvdbId, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var mappings = await db.SceneMappings
				.Where(m => m.TvdbId == tvdbId)
				.OrderBy(m => m.Id)
				.ToListAsync(cancellationToken);
			var episodeMappings = await db.SceneEpisodeMappings
				.Where(m => m.TvdbId == tvdbId)
				.OrderBy(m => m.Id)
				.ToListAsync(cancellationToken);
			return Results.Ok(new SceneMappingSetResource(
				tvdbId,
				mappings.Select(ToResource).ToList(),
				episodeMappings.Select(ToEpisodeResource).ToList()));
		});

		scene.MapGet("/{tvdbId:int}/resolve", async Task<IResult> (int tvdbId, int season, int episode, MappingResolver resolver, CancellationToken cancellationToken)
			=> Results.Ok(await resolver.ResolveSceneAsync(tvdbId, season, episode, cancellationToken)));

		scene.MapGet("/{tvdbId:int}/resolve-tvdb", async Task<IResult> (int tvdbId, int sceneSeason, int sceneEpisode, MappingResolver resolver, CancellationToken cancellationToken)
			=> Results.Ok(await resolver.ResolveTvdbAsync(tvdbId, sceneSeason, sceneEpisode, cancellationToken)));

		scene.MapGet("/names/{tvdbId:int}", async Task<IResult> (int tvdbId, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var names = await db.SceneNames
				.Where(n => n.TvdbId == tvdbId)
				.OrderBy(n => n.Id)
				.ToListAsync(cancellationToken);
			return Results.Ok(names.Select(ToNameResource).ToList());
		});

		scene.MapGet("/search", async Task<IResult> (string name, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var lowered = name.ToLower();

			var exactIds = await db.SceneNames
				.Where(n => n.SceneName.ToLower() == lowered)
				.Select(n => n.TvdbId)
				.Concat(db.SceneMappings.Where(m => m.Title.ToLower() == lowered).Select(m => m.TvdbId))
				.Distinct()
				.OrderBy(id => id)
				.ToListAsync(cancellationToken);
			if (exactIds.Count > 0)
			{
				return Results.Ok(exactIds);
			}

			var prefixIds = await db.SceneNames
				.Where(n => n.SceneName.ToLower().StartsWith(lowered))
				.Select(n => n.TvdbId)
				.Concat(db.SceneMappings.Where(m => m.Title.ToLower().StartsWith(lowered)).Select(m => m.TvdbId))
				.Distinct()
				.OrderBy(id => id)
				.ToListAsync(cancellationToken);
			return Results.Ok(prefixIds);
		});

		// A fresh group so the admin key filter only applies to the write endpoints below.
		var writes = app.MapGroup("/api/v1/scene").AddEndpointFilter<AdminApiKeyFilter>();

		writes.MapPost("/mappings", async Task<IResult> (
			CreateSceneMappingRequest request,
			MappingsDbContext db,
			IValidator<CreateSceneMappingRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			if (await HasSeasonConflict(db, request.TvdbId, request.SeasonNumber, excludeId: null, cancellationToken))
			{
				return SeasonConflict(request.TvdbId, request.SeasonNumber);
			}

			var entity = new SceneMapping
			{
				TvdbId = request.TvdbId,
				Title = request.Title,
				SeasonNumber = request.SeasonNumber,
				SceneSeasonNumber = request.SceneSeasonNumber,
				EpisodeOffset = request.EpisodeOffset,
				SearchTitle = request.SearchTitle,
				Comment = request.Comment,
			};
			db.SceneMappings.Add(entity);
			if (!await TrySave(db, cancellationToken))
			{
				return SeasonConflict(request.TvdbId, request.SeasonNumber);
			}

			return Results.Created($"/api/v1/scene/mappings/{entity.Id}", ToResource(entity));
		});

		writes.MapPut("/mappings/{id:int}", async Task<IResult> (
			int id,
			UpdateSceneMappingRequest request,
			MappingsDbContext db,
			IValidator<UpdateSceneMappingRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			var entity = await db.SceneMappings.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
			if (entity is null)
			{
				return NotFound(id);
			}

			if (await HasSeasonConflict(db, request.TvdbId, request.SeasonNumber, excludeId: id, cancellationToken))
			{
				return SeasonConflict(request.TvdbId, request.SeasonNumber);
			}

			entity.TvdbId = request.TvdbId;
			entity.Title = request.Title;
			entity.SeasonNumber = request.SeasonNumber;
			entity.SceneSeasonNumber = request.SceneSeasonNumber;
			entity.EpisodeOffset = request.EpisodeOffset;
			entity.SearchTitle = request.SearchTitle;
			entity.Comment = request.Comment;
			if (!await TrySave(db, cancellationToken))
			{
				return SeasonConflict(request.TvdbId, request.SeasonNumber);
			}

			return Results.Ok(ToResource(entity));
		});

		writes.MapDelete("/mappings/{id:int}", async Task<IResult> (int id, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var affected = await db.SceneMappings.Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken);
			return affected == 0 ? NotFound(id) : Results.NoContent();
		});

		writes.MapPost("/episode-mappings", async Task<IResult> (
			CreateSceneEpisodeMappingRequest request,
			MappingsDbContext db,
			IValidator<CreateSceneEpisodeMappingRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			var exists = await db.SceneEpisodeMappings.AnyAsync(
				m => m.TvdbId == request.TvdbId && m.SeasonNumber == request.SeasonNumber && m.EpisodeNumber == request.EpisodeNumber,
				cancellationToken);
			if (exists)
			{
				return EpisodeConflict(request.TvdbId, request.SeasonNumber, request.EpisodeNumber);
			}

			var entity = new SceneEpisodeMapping
			{
				TvdbId = request.TvdbId,
				SeasonNumber = request.SeasonNumber,
				EpisodeNumber = request.EpisodeNumber,
				SceneSeasonNumber = request.SceneSeasonNumber,
				SceneEpisodeNumber = request.SceneEpisodeNumber,
			};
			db.SceneEpisodeMappings.Add(entity);
			if (!await TrySave(db, cancellationToken))
			{
				return EpisodeConflict(request.TvdbId, request.SeasonNumber, request.EpisodeNumber);
			}

			return Results.Created($"/api/v1/scene/episode-mappings/{entity.Id}", ToEpisodeResource(entity));
		});

		writes.MapPut("/episode-mappings/{id:int}", async Task<IResult> (
			int id,
			UpdateSceneEpisodeMappingRequest request,
			MappingsDbContext db,
			IValidator<UpdateSceneEpisodeMappingRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			var entity = await db.SceneEpisodeMappings.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
			if (entity is null)
			{
				return NotFound(id);
			}

			var exists = await db.SceneEpisodeMappings.AnyAsync(
				m => m.Id != id
					&& m.TvdbId == request.TvdbId
					&& m.SeasonNumber == request.SeasonNumber
					&& m.EpisodeNumber == request.EpisodeNumber,
				cancellationToken);
			if (exists)
			{
				return EpisodeConflict(request.TvdbId, request.SeasonNumber, request.EpisodeNumber);
			}

			entity.TvdbId = request.TvdbId;
			entity.SeasonNumber = request.SeasonNumber;
			entity.EpisodeNumber = request.EpisodeNumber;
			entity.SceneSeasonNumber = request.SceneSeasonNumber;
			entity.SceneEpisodeNumber = request.SceneEpisodeNumber;
			if (!await TrySave(db, cancellationToken))
			{
				return EpisodeConflict(request.TvdbId, request.SeasonNumber, request.EpisodeNumber);
			}

			return Results.Ok(ToEpisodeResource(entity));
		});

		writes.MapDelete("/episode-mappings/{id:int}", async Task<IResult> (int id, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var affected = await db.SceneEpisodeMappings.Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken);
			return affected == 0 ? NotFound(id) : Results.NoContent();
		});

		writes.MapPost("/names", async Task<IResult> (
			CreateSceneNameRequest request,
			MappingsDbContext db,
			IValidator<CreateSceneNameRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			var exists = await db.SceneNames.AnyAsync(
				n => n.TvdbId == request.TvdbId && n.SceneName == request.SceneName,
				cancellationToken);
			if (exists)
			{
				return NameConflict(request.TvdbId, request.SceneName);
			}

			var entity = new SceneNameEntry { TvdbId = request.TvdbId, SceneName = request.SceneName, SeasonNumber = request.SeasonNumber };
			db.SceneNames.Add(entity);
			if (!await TrySave(db, cancellationToken))
			{
				return NameConflict(request.TvdbId, request.SceneName);
			}

			return Results.Created($"/api/v1/scene/names/{entity.Id}", ToNameResource(entity));
		});

		writes.MapPut("/names/{id:int}", async Task<IResult> (
			int id,
			UpdateSceneNameRequest request,
			MappingsDbContext db,
			IValidator<UpdateSceneNameRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			var entity = await db.SceneNames.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
			if (entity is null)
			{
				return NotFound(id);
			}

			var exists = await db.SceneNames.AnyAsync(
				n => n.Id != id && n.TvdbId == request.TvdbId && n.SceneName == request.SceneName,
				cancellationToken);
			if (exists)
			{
				return NameConflict(request.TvdbId, request.SceneName);
			}

			entity.TvdbId = request.TvdbId;
			entity.SceneName = request.SceneName;
			entity.SeasonNumber = request.SeasonNumber;
			if (!await TrySave(db, cancellationToken))
			{
				return NameConflict(request.TvdbId, request.SceneName);
			}

			return Results.Ok(ToNameResource(entity));
		});

		writes.MapDelete("/names/{id:int}", async Task<IResult> (int id, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var affected = await db.SceneNames.Where(n => n.Id == id).ExecuteDeleteAsync(cancellationToken);
			return affected == 0 ? NotFound(id) : Results.NoContent();
		});

		return app;
	}

	/// <summary>
	/// True when another mapping for the same series already covers the same season.
	/// Wildcard rows (null season) count as one value, the database unique index treats them as distinct.
	/// </summary>
	public static async Task<bool> HasSeasonConflict(MappingsDbContext db, int tvdbId, int? seasonNumber, int? excludeId, CancellationToken cancellationToken)
	{
		var seasons = await db.SceneMappings
			.Where(m => m.TvdbId == tvdbId && (excludeId == null || m.Id != excludeId))
			.Select(m => m.SeasonNumber)
			.ToListAsync(cancellationToken);
		return seasons.Any(s => s == seasonNumber);
	}

	/// <summary>
	/// Saves and reports unique constraint races as a conflict instead of throwing.
	/// </summary>
	public static async Task<bool> TrySave(MappingsDbContext db, CancellationToken cancellationToken)
	{
		try
		{
			await db.SaveChangesAsync(cancellationToken);
			return true;
		}
		catch (DbUpdateException)
		{
			return false;
		}
	}

	public static IResult NotFound(int id) => Results.Problem(
		statusCode: StatusCodes.Status404NotFound,
		title: "Not found",
		detail: $"No mapping with id {id} exists.");

	public static IResult SeasonConflict(int tvdbId, int? seasonNumber) => Results.Problem(
		statusCode: StatusCodes.Status409Conflict,
		title: "Mapping conflict",
		detail: $"A scene mapping for TVDB series {tvdbId} and season {(seasonNumber?.ToString() ?? "all seasons")} already exists.");

	public static IResult EpisodeConflict(int tvdbId, int seasonNumber, int episodeNumber) => Results.Problem(
		statusCode: StatusCodes.Status409Conflict,
		title: "Mapping conflict",
		detail: $"An episode mapping for TVDB series {tvdbId}, season {seasonNumber}, episode {episodeNumber} already exists.");

	public static IResult NameConflict(int tvdbId, string sceneName) => Results.Problem(
		statusCode: StatusCodes.Status409Conflict,
		title: "Mapping conflict",
		detail: $"Scene name '{sceneName}' already exists for TVDB series {tvdbId}.");

	public static SceneMappingResource ToResource(SceneMapping m) => new(
		m.Id,
		m.TvdbId,
		m.Title,
		m.SeasonNumber,
		m.SceneSeasonNumber,
		m.EpisodeOffset,
		m.SearchTitle,
		m.Comment);

	public static SceneEpisodeMappingResource ToEpisodeResource(SceneEpisodeMapping m) => new(
		m.Id,
		m.TvdbId,
		m.SeasonNumber,
		m.EpisodeNumber,
		m.SceneSeasonNumber,
		m.SceneEpisodeNumber);

	public static SceneNameResource ToNameResource(SceneNameEntry n) => new(n.Id, n.TvdbId, n.SceneName, n.SeasonNumber);
}
