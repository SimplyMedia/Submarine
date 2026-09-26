using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Config;

/// <summary>
///     Import list behaviour configuration.
/// </summary>
public sealed class ImportListConfigModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/config/import-list");
		group.MapGet("/", GetAsync);
		group.MapPut("/", PutAsync);
	}

	private static async Task<Ok<ImportListConfigResource>> GetAsync(SubmarineDbContext db, CancellationToken cancellationToken)
		=> TypedResults.Ok(ImportListConfigResource.FromEntity(await db.ImportListConfig.AsNoTracking().SingleAsync(cancellationToken)));

	private static async Task<Ok<ImportListConfigResource>> PutAsync(
		SubmarineDbContext db,
		IValidator<ImportListConfigResource> validator,
		ImportListConfigResource request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var config = await db.ImportListConfig.SingleAsync(cancellationToken);
		config.CleanLibraryLevel = request.CleanLibraryLevel;
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(ImportListConfigResource.FromEntity(config));
	}
}

/// <summary>Import list behaviour configuration resource.</summary>
/// <param name="CleanLibraryLevel">
///     What to do with library items no longer covered by any automatic-add import list.
/// </param>
public sealed record ImportListConfigResource(CleanLibraryLevel CleanLibraryLevel)
{
	/// <summary>Maps the singleton to the resource.</summary>
	public static ImportListConfigResource FromEntity(Core.Entities.ImportListConfig config)
		=> new(config.CleanLibraryLevel);
}

/// <summary>Validator for <see cref="ImportListConfigResource" /> PUT requests.</summary>
public sealed class ImportListConfigResourceValidator : AbstractValidator<ImportListConfigResource>
{
	/// <inheritdoc />
	public ImportListConfigResourceValidator()
	{
		RuleFor(x => x.CleanLibraryLevel).IsInEnum();
	}
}
