using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="ImportList" />.</summary>
internal sealed class ImportListConfiguration : EntityConfiguration<ImportList>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<ImportList> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="ImportListExclusion" />.</summary>
internal sealed class ImportListExclusionConfiguration : EntityConfiguration<ImportListExclusion>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<ImportListExclusion> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Title).HasMaxLength(512);
	}
}

/// <summary>Configuration for <see cref="ImportListStatus" />.</summary>
internal sealed class ImportListStatusConfiguration : EntityConfiguration<ImportListStatus>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<ImportListStatus> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.ImportListId).IsUnique();
		builder.HasOne(x => x.ImportList).WithMany().HasForeignKey(x => x.ImportListId).OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>Configuration for <see cref="Notification" />.</summary>
internal sealed class NotificationConfiguration : EntityConfiguration<Notification>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Notification> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.HasMany(x => x.Tags).WithMany();
	}
}
