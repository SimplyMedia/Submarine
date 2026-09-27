using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.QualityDefinitions;

/// <summary>Native quality-definition updates shared by native and compatibility transports.</summary>
public sealed class QualityDefinitionService(SubmarineDbContext db)
{
	public async Task<IReadOnlyList<QualityDefinition>> UpdateAsync(
		IReadOnlyList<QualityDefinitionUpdate> updates,
		CancellationToken cancellationToken)
	{
		var definitions = await db.QualityDefinitions.ToDictionaryAsync(definition => definition.Id, cancellationToken);
		foreach (var update in updates)
		{
			if (!definitions.TryGetValue(update.Id, out var definition))
				throw new KeyNotFoundException($"Quality definition {update.Id} does not exist");
			if (update.MinSizeMbPerMinute is { } min && update.MaxSizeMbPerMinute is { } max && min > max)
				throw new FluentValidation.ValidationException($"Quality definition {update.Id}: min size must not exceed max size");
			}

		foreach (var update in updates)
		{
			var definition = definitions[update.Id];
			definition.MinSizeMbPerMinute = update.MinSizeMbPerMinute;
			definition.MaxSizeMbPerMinute = update.MaxSizeMbPerMinute;
			definition.PreferredSizeMbPerMinute = update.PreferredSizeMbPerMinute;
		}

		await db.SaveChangesAsync(cancellationToken);
		return updates.Select(update => definitions[update.Id]).ToArray();
	}
}
