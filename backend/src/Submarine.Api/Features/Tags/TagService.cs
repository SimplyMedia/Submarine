using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Tags;

public sealed class TagService(SubmarineDbContext db)
{
	public async Task<TagMutation> CreateAsync(string label, CancellationToken cancellationToken)
	{
		if (await db.Tags.AnyAsync(x => x.Label == label, cancellationToken))
		{
			return TagMutation.Conflict($"Tag '{label}' already exists");
		}

		var tag = new Tag { Label = label };
		db.Tags.Add(tag);
		await db.SaveChangesAsync(cancellationToken);
		return TagMutation.Success(tag);
	}

	public async Task<TagMutation> UpdateAsync(int id, string label, CancellationToken cancellationToken)
	{
		var tag = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (tag is null)
		{
			return TagMutation.NotFound($"Tag {id} not found");
		}

		if (await db.Tags.AnyAsync(x => x.Label == label && x.Id != id, cancellationToken))
		{
			return TagMutation.Conflict($"Tag '{label}' already exists");
		}

		tag.Label = label;
		await db.SaveChangesAsync(cancellationToken);
		return TagMutation.Success(tag);
	}

	public async Task<TagMutation> DeleteAsync(int id, CancellationToken cancellationToken)
	{
		var tag = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (tag is null)
		{
			return TagMutation.NotFound($"Tag {id} not found");
		}

		db.Tags.Remove(tag);
		await db.SaveChangesAsync(cancellationToken);
		return TagMutation.Success(tag);
	}
}

public sealed record TagMutation(Tag? Tag, int StatusCode, string? Error)
{
	public static TagMutation Success(Tag tag) => new(tag, StatusCodes.Status200OK, null);
	public static TagMutation Conflict(string error) => new(null, StatusCodes.Status409Conflict, error);
	public static TagMutation NotFound(string error) => new(null, StatusCodes.Status404NotFound, error);
}
