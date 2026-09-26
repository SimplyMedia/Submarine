using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Features.QualityProfiles;
using Submarine.Api.Modules;
using Submarine.Core.CustomFormats;
using Submarine.Core.Modules;
using Submarine.Core.Enums;
using Submarine.Core.Parser;
using Submarine.Core.Release;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.CustomFormats;

/// <summary>
///     Custom format CRUD, TRaSH import and export, and live testing.
/// </summary>
public sealed class CustomFormatsModule : IEndpointModule, IServiceModule
{
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<CustomFormatService>();
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/custom-formats");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPost("/import", ImportAsync);
		group.MapPost("/test", TestAsync);
		group.MapGet("/{id:int}/export", ExportAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<CustomFormatResource>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		// Specifications is a JSON column, so paging happens on materialized resources
		var formats = await db.CustomFormats
			.AsNoTracking()
			.OrderBy(format => format.Id)
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(await PagedResult<CustomFormatResource>.CreateAsync(
			formats.Select(CustomFormatResource.FromEntity).AsQueryable(),
			query,
			cancellationToken));
	}

	private static async Task<Results<Ok<CustomFormatResource>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var format = await db.CustomFormats.AsNoTracking().FirstOrDefaultAsync(format => format.Id == id, cancellationToken);

		return format is null ? TypedResults.NotFound() : TypedResults.Ok(CustomFormatResource.FromEntity(format));
	}

	private static async Task<Created<CustomFormatResource>> CreateAsync(
		CustomFormatService service,
		CustomFormatRequest request,
		CancellationToken cancellationToken)
	{
		var format = await service.CreateAsync(request, cancellationToken);
		return TypedResults.Created($"/api/v1/custom-formats/{format.Id}", CustomFormatResource.FromEntity(format));
	}

	private static async Task<Created<List<CustomFormatResource>>> ImportAsync(
		SubmarineDbContext db,
		JsonDocument body,
		CancellationToken cancellationToken)
	{
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		List<TrashFormat> formats;

		if (body.RootElement.ValueKind is JsonValueKind.Array)
		{
			formats = body.Deserialize<List<TrashFormat>>(options) ?? [];
		}
		else if (body.RootElement.ValueKind is JsonValueKind.Object)
		{
			formats = [body.Deserialize<TrashFormat>(options)!];
		}
		else
		{
			throw new FluentValidation.ValidationException("Expected a TRaSH format object or an array of formats");
		}

		if (formats.Any(format => format is null))
		{
			throw new FluentValidation.ValidationException("Expected a TRaSH format object or an array of formats");
		}

		foreach (var specification in formats.SelectMany(format => format.Specifications ?? [])
			         .Where(specification => !TrashCustomFormatJson.SupportedImplementations.Contains(specification.Implementation)))
		{
			throw new FluentValidation.ValidationException(
				$"Unknown custom format specification implementation '{specification.Implementation}'");
		}

		var imported = new List<CustomFormatResource>();

		foreach (var trashFormat in formats)
		{
			var format = TrashCustomFormatJson.FromTrash(trashFormat);
			await EnsureUniqueNameAsync(db, format.Name, null, cancellationToken);

			db.CustomFormats.Add(format);
			imported.Add(CustomFormatResource.FromEntity(format));
		}

		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created("/api/v1/custom-formats", imported);
	}

	private static async Task<Results<Ok<TrashFormat>, NotFound>> ExportAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var format = await db.CustomFormats.AsNoTracking().FirstOrDefaultAsync(format => format.Id == id, cancellationToken);

		return format is null
			? TypedResults.NotFound()
			: TypedResults.Ok(TrashCustomFormatJson.ToTrash(format));
	}

	private static async Task<Ok<TestCustomFormatResult>> TestAsync(
		SubmarineDbContext db,
		IParser<BaseRelease> parser,
		TestCustomFormatRequest request,
		CancellationToken cancellationToken)
	{
		BaseRelease parsed;
		try
		{
			parsed = parser.Parse(request.Title);
		}
		catch (Exception)
		{
			throw new FluentValidation.ValidationException("The title could not be parsed as a release");
		}

		var context = new ReleaseContext(request.Title, parsed, request.Size, parsed.Year);
		var formats = await db.CustomFormats.AsNoTracking().ToListAsync(cancellationToken);
		var matched = CustomFormatCalculator.Match(formats, context);

		return TypedResults.Ok(new TestCustomFormatResult(
			[.. matched.Select(CustomFormatResource.FromEntity)],
			matched.Sum(format => request.FormatItems?.FirstOrDefault(item => item.CustomFormatId == format.Id)?.Score ?? 0)));
	}

	private static async Task<Results<Ok<CustomFormatResource>, NotFound>> UpdateAsync(
		int id,
		CustomFormatService service,
		CustomFormatRequest request,
		CancellationToken cancellationToken)
	{
		var format = await service.UpdateAsync(id, request, cancellationToken);
		return format is null ? TypedResults.NotFound() : TypedResults.Ok(CustomFormatResource.FromEntity(format));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, CustomFormatService service, CancellationToken cancellationToken)
		=> await service.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

}

/// <summary>A custom format resource.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="IncludeCustomFormatWhenRenaming">Whether the format name appears in renamed files.</param>
/// <param name="Specifications">The specifications of the format.</param>
public sealed record CustomFormatResource(
	int Id,
	string Name,
	bool IncludeCustomFormatWhenRenaming,
	IReadOnlyList<SpecificationResource> Specifications)
{
	/// <summary>Maps an entity to the resource.</summary>
	public static CustomFormatResource FromEntity(Core.Entities.CustomFormat format)
		=> new(
			format.Id,
			format.Name,
			format.IncludeCustomFormatWhenRenaming,
			[.. format.Specifications.Select(specification => new SpecificationResource(
				specification.Name,
				specification.Type,
				specification.Negate,
				specification.Required,
				specification.Value))]);
}

/// <summary>One specification of a custom format resource or request.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Type">Specification type.</param>
/// <param name="Negate">Invert the match result.</param>
/// <param name="Required">Release must match for the format to apply.</param>
/// <param name="Value">Type specific value: string, boolean, or a min/max object for size and year.</param>
public sealed record SpecificationResource(
	string Name,
	Submarine.Core.Enums.CustomFormatSpecificationType Type,
	bool Negate,
	bool Required,
	JsonElement? Value);

/// <summary>Create or update request for a custom format.</summary>
/// <param name="Name">Name, unique.</param>
/// <param name="IncludeCustomFormatWhenRenaming">Whether the format name appears in renamed files.</param>
/// <param name="Specifications">The specifications of the format.</param>
public sealed record CustomFormatRequest(
	string Name,
	bool IncludeCustomFormatWhenRenaming,
	IReadOnlyList<SpecificationResource> Specifications);

/// <summary>Request body for testing a title against the custom formats.</summary>
/// <param name="Title">The release title to test.</param>
/// <param name="Size">Release size in bytes, if known.</param>
/// <param name="FormatItems">Optional profile scores used to compute the total score.</param>
public sealed record TestCustomFormatRequest(
	string Title,
	long? Size,
	IReadOnlyList<FormatItemResource>? FormatItems);

/// <summary>Result of testing a title against the custom formats.</summary>
/// <param name="Matched">The formats matching the title.</param>
/// <param name="Score">The total score of the matched formats given the supplied format items.</param>
public sealed record TestCustomFormatResult(IReadOnlyList<CustomFormatResource> Matched, int Score);

/// <summary>Validator for <see cref="CustomFormatRequest" />.</summary>
public sealed class CustomFormatRequestValidator : AbstractValidator<CustomFormatRequest>
{
	/// <inheritdoc />
	public CustomFormatRequestValidator()
	{
		RuleFor(request => request.Name)
			.NotEmpty()
			.MaximumLength(200);

		RuleForEach(request => request.Specifications)
			.ChildRules(specification => specification.RuleFor(entry => entry.Name).NotEmpty());
	}
}
