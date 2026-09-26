using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence.Converters;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="MetadataConsumer" />.</summary>
internal sealed class MetadataConsumerConfiguration : EntityConfiguration<MetadataConsumer>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<MetadataConsumer> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
	}
}

/// <summary>Configuration for <see cref="AutoTaggingRule" />.</summary>
internal sealed class AutoTaggingRuleConfiguration : EntityConfiguration<AutoTaggingRule>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<AutoTaggingRule> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.Specifications).HasConversion(JsonValueConverter<List<AutoTaggingSpecification>>.Instance);
		builder.HasMany(x => x.Tags).WithMany();
	}
}
