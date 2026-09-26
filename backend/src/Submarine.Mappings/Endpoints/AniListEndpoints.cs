using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Data;
using Submarine.Mappings.Entities;
using Submarine.Mappings.Infrastructure;
using Submarine.Mappings.Services;

namespace Submarine.Mappings.Endpoints;

/// <summary>
/// AniList arc mapping endpoints: lookup, both resolution directions and CRUD.
/// </summary>
public static class AniListEndpoints
{
	public static IEndpointRouteBuilder MapAniListEndpoints(this IEndpointRouteBuilder app)
	{
		var anilist = app.MapGroup("/api/v1/anilist");

		anilist.MapGet("/{aniListId:int}", async Task<IResult> (int aniListId, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var mapping = await db.AniListMappings.FirstOrDefaultAsync(m => m.AniListId == aniListId, cancellationToken);
			return mapping is null ? NotFound(aniListId) : Results.Ok(AniListMappingService.ToResource(mapping));
		});

		anilist.MapGet("/{aniListId:int}/resolve", async Task<IResult> (
			int aniListId,
			int episode,
			AniListMappingService service,
			CancellationToken cancellationToken) =>
		{
			var resolution = await service.ResolveTvdbAsync(aniListId, episode, cancellationToken);
			return resolution is null
				? Results.Problem(
					statusCode: StatusCodes.Status404NotFound,
					title: "Not found",
					detail: $"AniList entry {aniListId} does not exist or episode {episode} is outside the arc.")
				: Results.Ok(resolution);
		});

		var tvdb = app.MapGroup("/api/v1/tvdb/{tvdbId:int}/anilist");

		tvdb.MapGet("/", async (int tvdbId, AniListMappingService service, CancellationToken cancellationToken)
			=> Results.Ok(await service.GetForSeriesAsync(tvdbId, cancellationToken)));

		tvdb.MapGet("/resolve", async Task<IResult> (
			int tvdbId,
			int season,
			int episode,
			AniListMappingService service,
			CancellationToken cancellationToken) =>
		{
			var resolution = await service.ResolveAniListAsync(tvdbId, season, episode, cancellationToken);
			return resolution is null
				? Results.Problem(
					statusCode: StatusCodes.Status404NotFound,
					title: "Not found",
					detail: $"No AniList entry covers TVDB series {tvdbId}, season {season}, episode {episode}.")
				: Results.Ok(resolution);
		});

		// A fresh group so the admin key filter only applies to the write endpoints below.
		var writes = app.MapGroup("/api/v1/anilist").AddEndpointFilter<AdminApiKeyFilter>();

		writes.MapPost("/", async Task<IResult> (
			CreateAniListMappingRequest request,
			MappingsDbContext db,
			IValidator<CreateAniListMappingRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			var exists = await db.AniListMappings.AnyAsync(m => m.AniListId == request.AniListId, cancellationToken);
			if (exists)
			{
				return Conflict(request.AniListId);
			}

			var entity = new AniListMapping
			{
				AniListId = request.AniListId,
				TvdbId = request.TvdbId,
				Title = request.Title,
				TvdbSeason = request.TvdbSeason,
				EpisodeStart = request.EpisodeStart,
				EpisodeCount = request.EpisodeCount,
				AbsoluteOffset = request.AbsoluteOffset,
			};
			db.AniListMappings.Add(entity);
			if (!await SceneEndpoints.TrySave(db, cancellationToken))
			{
				return Conflict(request.AniListId);
			}

			return Results.Created($"/api/v1/anilist/{entity.AniListId}", AniListMappingService.ToResource(entity));
		});

		writes.MapPut("/{id:int}", async Task<IResult> (
			int id,
			UpdateAniListMappingRequest request,
			MappingsDbContext db,
			IValidator<UpdateAniListMappingRequest> validator,
			CancellationToken cancellationToken) =>
		{
			var invalid = await validator.ValidateOrNullAsync(request, cancellationToken);
			if (invalid is not null)
			{
				return invalid;
			}

			var entity = await db.AniListMappings.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
			if (entity is null)
			{
				return SceneEndpoints.NotFound(id);
			}

			var exists = await db.AniListMappings.AnyAsync(m => m.Id != id && m.AniListId == request.AniListId, cancellationToken);
			if (exists)
			{
				return Conflict(request.AniListId);
			}

			entity.AniListId = request.AniListId;
			entity.TvdbId = request.TvdbId;
			entity.Title = request.Title;
			entity.TvdbSeason = request.TvdbSeason;
			entity.EpisodeStart = request.EpisodeStart;
			entity.EpisodeCount = request.EpisodeCount;
			entity.AbsoluteOffset = request.AbsoluteOffset;
			if (!await SceneEndpoints.TrySave(db, cancellationToken))
			{
				return Conflict(request.AniListId);
			}

			return Results.Ok(AniListMappingService.ToResource(entity));
		});

		writes.MapDelete("/{id:int}", async Task<IResult> (int id, MappingsDbContext db, CancellationToken cancellationToken) =>
		{
			var affected = await db.AniListMappings.Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken);
			return affected == 0 ? SceneEndpoints.NotFound(id) : Results.NoContent();
		});

		return app;
	}

	public static IResult NotFound(int aniListId) => Results.Problem(
		statusCode: StatusCodes.Status404NotFound,
		title: "Not found",
		detail: $"No AniList mapping with id {aniListId} exists.");

	public static IResult Conflict(int aniListId) => Results.Problem(
		statusCode: StatusCodes.Status409Conflict,
		title: "Mapping conflict",
		detail: $"An AniList mapping for AniList id {aniListId} already exists.");
}
