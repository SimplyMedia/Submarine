using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Submarine.Api.Common;
using Submarine.Api.Features.Series;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Tags;

/// <summary>
///     Tag endpoints with a per-tag usage detail.
/// </summary>
public sealed class TagsModule : IServiceModule, IEndpointModule
{
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<TagService>();

	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/tags");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapGet("/{id:int}/detail", DetailAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<TagDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(await db.Tags.AsNoTracking()
			.OrderBy(x => x.Label)
			.Select(x => new TagDto(x.Id, x.Label))
			.ToListAsync(cancellationToken));

	private static async Task<Ok<TagDto>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(await db.Tags.AsNoTracking()
			.Where(x => x.Id == id)
			.Select(x => new TagDto(x.Id, x.Label))
			.FirstOrDefaultAsync(cancellationToken)
			?? throw new KeyNotFoundException($"Tag {id} not found"));

	private static async Task<Ok<TagDetailDto>> DetailAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		_ = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Tag {id} not found");
		return TypedResults.Ok(new TagDetailDto(
			id,
			await db.Series.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken),
			await db.Movies.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken),
			await db.Indexers.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken),
			await db.Notifications.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken),
			await db.DelayProfiles.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken),
			await db.ReleaseProfiles.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken),
			await db.ImportLists.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken),
			await db.AutoTaggingRules.Where(x => x.Tags.Any(t => t.Id == id)).Select(x => x.Id).ToListAsync(cancellationToken)));
	}

	private static async Task<Results<Created<TagDto>, ProblemHttpResult>> CreateAsync(
		TagService service,
		IValidator<TagRequest> validator,
		[FromBody] TagRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var result = await service.CreateAsync(request.Label, cancellationToken);
		return result.Tag is null
			? TypedResults.Problem(statusCode: result.StatusCode, title: result.Error)
			: TypedResults.Created($"/api/v1/tags/{result.Tag.Id}", new TagDto(result.Tag.Id, result.Tag.Label));
	}

	private static async Task<Results<Ok<TagDto>, ProblemHttpResult>> UpdateAsync(
		int id,
		TagService service,
		IValidator<TagRequest> validator,
		[FromBody] TagRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var result = await service.UpdateAsync(id, request.Label, cancellationToken);
		return result.Tag is null
			? TypedResults.Problem(statusCode: result.StatusCode, title: result.Error)
			: TypedResults.Ok(new TagDto(result.Tag.Id, result.Tag.Label));
	}

	private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
		int id,
		TagService service,
		CancellationToken cancellationToken)
	{
		var result = await service.DeleteAsync(id, cancellationToken);
		return result.Tag is null
			? TypedResults.Problem(statusCode: result.StatusCode, title: result.Error)
			: TypedResults.NoContent();
	}
}

/// <summary>Create or update tag request.</summary>
/// <param name="Label">Unique label.</param>
public sealed record TagRequest(string Label);

/// <summary>Validator for <see cref="TagRequest" />.</summary>
public sealed class TagRequestValidator : AbstractValidator<TagRequest>
{
	/// <inheritdoc />
	public TagRequestValidator()
	{
		RuleFor(x => x.Label).NotEmpty().MaximumLength(256);
	}
}

/// <summary>Usage detail of a tag.</summary>
/// <param name="Id">Tag id.</param>
/// <param name="SeriesIds">Series using the tag.</param>
/// <param name="MovieIds">Movies using the tag.</param>
/// <param name="IndexerIds">Indexers using the tag.</param>
/// <param name="NotificationIds">Notifications using the tag.</param>
/// <param name="DelayProfileIds">Delay profiles using the tag.</param>
/// <param name="ReleaseProfileIds">Release profiles using the tag.</param>
/// <param name="ImportListIds">Import lists using the tag.</param>
/// <param name="AutoTaggingRuleIds">Auto tagging rules using the tag.</param>
public sealed record TagDetailDto(
	int Id,
	List<int> SeriesIds,
	List<int> MovieIds,
	List<int> IndexerIds,
	List<int> NotificationIds,
	List<int> DelayProfileIds,
	List<int> ReleaseProfileIds,
	List<int> ImportListIds,
	List<int> AutoTaggingRuleIds);
