using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.AutoTagging;

/// <summary>
///     Auto tagging rules: CRUD. Rules are applied when a series or movie is added or refreshed.
/// </summary>
public sealed class AutoTaggingModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/auto-tagging");
		group.MapGet("/", ListAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<AutoTaggingRuleDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var rules = await db.AutoTaggingRules.AsNoTracking().Include(x => x.Tags).OrderBy(x => x.Id).ToListAsync(cancellationToken);
		return TypedResults.Ok(rules.Select(ToDto).ToList());
	}

	private static async Task<Created<AutoTaggingRuleDto>> CreateAsync(
		SubmarineDbContext db,
		IValidator<SaveAutoTaggingRuleRequest> validator,
		SaveAutoTaggingRuleRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var rule = new AutoTaggingRule();
		Apply(rule, request);
		rule.Tags = await ResolveTagsAsync(db, request.Tags, cancellationToken);
		db.AutoTaggingRules.Add(rule);
		await db.SaveChangesAsync(cancellationToken);
		await db.Entry(rule).Collection(x => x.Tags).LoadAsync(cancellationToken);
		return TypedResults.Created($"/api/v1/auto-tagging/{rule.Id}", ToDto(rule));
	}

	private static async Task<Ok<AutoTaggingRuleDto>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<SaveAutoTaggingRuleRequest> validator,
		SaveAutoTaggingRuleRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var rule = await db.AutoTaggingRules.Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Auto tagging rule {id} does not exist");
		Apply(rule, request);
		rule.Tags = await ResolveTagsAsync(db, request.Tags, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ToDto(rule));
	}

	private static async Task<NoContent> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var rule = await db.AutoTaggingRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Auto tagging rule {id} does not exist");
		db.AutoTaggingRules.Remove(rule);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static AutoTaggingRuleDto ToDto(AutoTaggingRule rule)
		=> new(
			rule.Id,
			rule.Name,
			rule.Enable,
			rule.RemoveTagsAutomatically,
			rule.Specifications,
			rule.Tags.Select(x => x.Label).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());

	private static void Apply(AutoTaggingRule rule, SaveAutoTaggingRuleRequest request)
	{
		rule.Name = request.Name;
		rule.Enable = request.Enable;
		rule.RemoveTagsAutomatically = request.RemoveTagsAutomatically;
		rule.Specifications = [.. request.Specifications];
	}

	private static async Task<List<Tag>> ResolveTagsAsync(SubmarineDbContext db, IReadOnlyList<string> labels, CancellationToken cancellationToken)
	{
		var resolved = new List<Tag>();
		foreach (var label in labels.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var tag = await db.Tags.FirstOrDefaultAsync(x => x.Label == label, cancellationToken);
			if (tag is null)
			{
				tag = new Tag { Label = label };
				db.Tags.Add(tag);
			}

			resolved.Add(tag);
		}

		return resolved;
	}
}

/// <summary>Create or replace an auto tagging rule request.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Enable">Whether the rule is active.</param>
/// <param name="RemoveTagsAutomatically">Remove the tags automatically when the item no longer matches.</param>
/// <param name="Specifications">Specifications all or some of which must match, depending on Required flags.</param>
/// <param name="Tags">Tag labels applied when the rule matches.</param>
public sealed record SaveAutoTaggingRuleRequest(
	string Name,
	bool Enable,
	bool RemoveTagsAutomatically,
	IReadOnlyList<AutoTaggingSpecification> Specifications,
	IReadOnlyList<string> Tags);

/// <summary>A saved auto tagging rule.</summary>
public sealed record AutoTaggingRuleDto(
	int Id,
	string Name,
	bool Enable,
	bool RemoveTagsAutomatically,
	IReadOnlyList<AutoTaggingSpecification> Specifications,
	IReadOnlyList<string> Tags);

/// <summary>Validator for <see cref="SaveAutoTaggingRuleRequest" />.</summary>
public sealed class SaveAutoTaggingRuleRequestValidator : AbstractValidator<SaveAutoTaggingRuleRequest>
{
	/// <inheritdoc />
	public SaveAutoTaggingRuleRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
		RuleForEach(x => x.Specifications).ChildRules(spec => spec.RuleFor(x => x.Type).IsInEnum());
	}
}
