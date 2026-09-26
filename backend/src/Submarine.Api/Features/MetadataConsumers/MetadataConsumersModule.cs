using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.MetadataConsumers;

/// <summary>
///     Metadata consumers: CRUD and schema. Each consumer writes companion NFO/XML files and images next to
///     imported media for one external media center.
/// </summary>
public sealed class MetadataConsumersModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/metadata-consumers");
		group.MapGet("/", ListAsync);
		group.MapGet("/schema", SchemaAsync);
		group.MapPost("/", CreateAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<List<MetadataConsumerDto>>> ListAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var consumers = await db.MetadataConsumers.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
		return TypedResults.Ok(consumers.Select(ToDto).ToList());
	}

	private static Ok<List<MetadataConsumerSchemaDto>> SchemaAsync()
		=> TypedResults.Ok(MetadataConsumerSettingsJson.DescribeAll()
			.Select(pair => new MetadataConsumerSchemaDto(pair.Key.ToString(), pair.Value))
			.ToList());

	private static async Task<Created<MetadataConsumerDto>> CreateAsync(
		SubmarineDbContext db,
		IValidator<SaveMetadataConsumerRequest> validator,
		SaveMetadataConsumerRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var consumer = new MetadataConsumer();
		Apply(consumer, request);
		db.MetadataConsumers.Add(consumer);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Created($"/api/v1/metadata-consumers/{consumer.Id}", ToDto(consumer));
	}

	private static async Task<Ok<MetadataConsumerDto>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<SaveMetadataConsumerRequest> validator,
		SaveMetadataConsumerRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var consumer = await db.MetadataConsumers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Metadata consumer {id} does not exist");
		Apply(consumer, request);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ToDto(consumer));
	}

	private static async Task<NoContent> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var consumer = await db.MetadataConsumers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Metadata consumer {id} does not exist");
		db.MetadataConsumers.Remove(consumer);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static MetadataConsumerDto ToDto(MetadataConsumer consumer)
		=> new(consumer.Id, consumer.Name, consumer.Type.ToString(), consumer.Enable, consumer.SettingsJson);

	private static void Apply(MetadataConsumer consumer, SaveMetadataConsumerRequest request)
	{
		consumer.Name = request.Name;
		consumer.Type = request.Type;
		consumer.Enable = request.Enable;
		consumer.SettingsJson = request.SettingsJson;
	}
}

/// <summary>Create or replace a metadata consumer request.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Type">Consumer implementation.</param>
/// <param name="Enable">Whether the consumer is active.</param>
/// <param name="SettingsJson">Implementation settings as JSON.</param>
public sealed record SaveMetadataConsumerRequest(string Name, MetadataConsumerType Type, bool Enable = true, string SettingsJson = "{}");

/// <summary>A saved metadata consumer.</summary>
public sealed record MetadataConsumerDto(int Id, string Name, string Type, bool Enable, string SettingsJson);

/// <summary>Field descriptors of one metadata consumer type.</summary>
/// <param name="Type">Consumer implementation name.</param>
/// <param name="Fields">Field descriptors.</param>
public sealed record MetadataConsumerSchemaDto(string Type, IReadOnlyList<MetadataConsumerFieldDescriptor> Fields);

/// <summary>Validator for <see cref="SaveMetadataConsumerRequest" />.</summary>
public sealed class SaveMetadataConsumerRequestValidator : AbstractValidator<SaveMetadataConsumerRequest>
{
	/// <inheritdoc />
	public SaveMetadataConsumerRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
		RuleFor(x => x.Type).IsInEnum();
		RuleFor(x => x.SettingsJson).NotEmpty().Must(BeJsonObject).WithMessage("must be a JSON object");
	}

	private static bool BeJsonObject(string json)
		=> json.StartsWith('{') && json.EndsWith('}');
}
