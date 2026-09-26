using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.RemotePathMappings;

/// <summary>
///     Remote path mapping CRUD: translates download client host paths to local paths.
/// </summary>
public sealed class RemotePathMappingsModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/remote-path-mappings");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<RemotePathMappingDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var mappings = await db.RemotePathMappings.AsNoTracking().OrderBy(x => x.Host).ToListAsync(cancellationToken);
		return TypedResults.Ok(await PagedResult<RemotePathMappingDto>.CreateAsync(
			mappings.Select(ToDto).AsQueryable(),
			query,
			cancellationToken));
	}

	private static async Task<Results<Ok<RemotePathMappingDto>, NotFound>> GetAsync(
		int id,
		SubmarineDbContext db,
		CancellationToken cancellationToken)
	{
		var mapping = await db.RemotePathMappings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		return mapping is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(mapping));
	}

	private static async Task<Created<RemotePathMappingDto>> CreateAsync(
		SubmarineDbContext db,
		IValidator<RemotePathMappingRequest> validator,
		[FromBody] RemotePathMappingRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var mapping = new RemotePathMapping();
		Apply(mapping, request);

		db.RemotePathMappings.Add(mapping);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/remote-path-mappings/{mapping.Id}", ToDto(mapping));
	}

	private static async Task<Results<Ok<RemotePathMappingDto>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<RemotePathMappingRequest> validator,
		[FromBody] RemotePathMappingRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var mapping = await db.RemotePathMappings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (mapping is null)
		{
			return TypedResults.NotFound();
		}

		Apply(mapping, request);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ToDto(mapping));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var mapping = await db.RemotePathMappings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (mapping is null)
		{
			return TypedResults.NotFound();
		}

		db.RemotePathMappings.Remove(mapping);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static void Apply(RemotePathMapping mapping, RemotePathMappingRequest request)
	{
		mapping.Host = request.Host;
		mapping.RemotePath = request.RemotePath;
		mapping.LocalPath = request.LocalPath;
	}

	private static RemotePathMappingDto ToDto(RemotePathMapping mapping)
		=> new(mapping.Id, mapping.Host, mapping.RemotePath, mapping.LocalPath);
}

/// <summary>A remote path mapping.</summary>
/// <param name="Id">Id.</param>
/// <param name="Host">Host the mapping applies to.</param>
/// <param name="RemotePath">Remote path prefix.</param>
/// <param name="LocalPath">Local path prefix.</param>
public sealed record RemotePathMappingDto(int Id, string Host, string RemotePath, string LocalPath);

/// <summary>Create or update request for a remote path mapping.</summary>
/// <param name="Host">Host the mapping applies to.</param>
/// <param name="RemotePath">Remote path prefix.</param>
/// <param name="LocalPath">Local path prefix.</param>
public sealed record RemotePathMappingRequest(string Host, string RemotePath, string LocalPath);

/// <summary>Validator for <see cref="RemotePathMappingRequest" />.</summary>
public sealed class RemotePathMappingRequestValidator : AbstractValidator<RemotePathMappingRequest>
{
	/// <inheritdoc />
	public RemotePathMappingRequestValidator()
	{
		RuleFor(x => x.Host).NotEmpty().MaximumLength(512);
		RuleFor(x => x.RemotePath).NotEmpty().MaximumLength(1024);
		RuleFor(x => x.LocalPath).NotEmpty().MaximumLength(1024);
	}
}
