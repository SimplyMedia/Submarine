using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Core.Common;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.CustomFormats;

/// <summary>Native custom-format mutations shared by native and compatibility transports.</summary>
public sealed class CustomFormatService(SubmarineDbContext db, IValidator<CustomFormatRequest> validator)
{
	public async Task<Core.Entities.CustomFormat> CreateAsync(CustomFormatRequest request, CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		await EnsureUniqueNameAsync(request.Name, null, cancellationToken);
		var format = new Core.Entities.CustomFormat();
		ApplyRequest(format, request);
		db.CustomFormats.Add(format);
		await db.SaveChangesAsync(cancellationToken);
		return format;
	}

	public async Task<Core.Entities.CustomFormat?> UpdateAsync(int id, CustomFormatRequest request, CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);
		var format = await db.CustomFormats.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
		if (format is null) return null;
		await EnsureUniqueNameAsync(request.Name, id, cancellationToken);
		ApplyRequest(format, request);
		await db.SaveChangesAsync(cancellationToken);
		return format;
	}

	public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
	{
		var format = await db.CustomFormats.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
		if (format is null) return false;
		db.CustomFormats.Remove(format);
		await db.SaveChangesAsync(cancellationToken);
		return true;
	}

	private async Task EnsureUniqueNameAsync(string name, int? excludeId, CancellationToken cancellationToken)
	{
		if (await db.CustomFormats.AnyAsync(format => format.Name == name && (excludeId == null || format.Id != excludeId), cancellationToken))
			throw new ConflictException($"A custom format named '{name}' already exists");
	}

	private static void ApplyRequest(Core.Entities.CustomFormat format, CustomFormatRequest request)
	{
		format.Name = request.Name;
		format.IncludeCustomFormatWhenRenaming = request.IncludeCustomFormatWhenRenaming;
		format.Specifications = [.. request.Specifications.Select(specification => new Core.Entities.CustomFormatSpecification(
			specification.Name, specification.Type, specification.Negate, specification.Required, specification.Value))];
	}
}
