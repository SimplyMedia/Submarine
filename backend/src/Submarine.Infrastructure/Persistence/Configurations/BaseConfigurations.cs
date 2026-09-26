using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>
///     Base configuration for timestamped entities.
/// </summary>
internal abstract class EntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
	where TEntity : Entity
{
	/// <inheritdoc />
	public virtual void Configure(EntityTypeBuilder<TEntity> builder)
		=> builder.HasKey(x => x.Id);
}

/// <summary>
///     Base configuration for singleton rows with a fixed Id of 1.
/// </summary>
internal abstract class SingletonEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
	where TEntity : SingletonEntity
{
	/// <inheritdoc />
	public virtual void Configure(EntityTypeBuilder<TEntity> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).ValueGeneratedNever();
	}
}
