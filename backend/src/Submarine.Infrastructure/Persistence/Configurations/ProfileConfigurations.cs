using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Infrastructure.Persistence.Converters;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="QualityProfile" />.</summary>
internal sealed class QualityProfileConfiguration : EntityConfiguration<QualityProfile>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<QualityProfile> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.Items).HasConversion(JsonValueConverter<List<QualityProfileItem>>.Instance);
		builder.Property(x => x.FormatItems).HasConversion(JsonValueConverter<List<ProfileFormatItem>>.Instance);
	}
}

/// <summary>Configuration for <see cref="QualityDefinition" />.</summary>
internal sealed class QualityDefinitionConfiguration : EntityConfiguration<QualityDefinition>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<QualityDefinition> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Title).HasMaxLength(128);
		builder.HasIndex(x => new { x.Source, x.Resolution }).IsUnique();
	}
}

/// <summary>Configuration for <see cref="LanguageProfile" />.</summary>
internal sealed class LanguageProfileConfiguration : EntityConfiguration<LanguageProfile>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<LanguageProfile> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.Languages).HasConversion(JsonValueConverter<List<Language>>.Instance);
	}
}

/// <summary>Configuration for <see cref="DelayProfile" />.</summary>
internal sealed class DelayProfileConfiguration : EntityConfiguration<DelayProfile>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<DelayProfile> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="ReleaseProfile" />.</summary>
internal sealed class ReleaseProfileConfiguration : EntityConfiguration<ReleaseProfile>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<ReleaseProfile> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.Required).HasConversion(JsonValueConverter<List<string>>.Instance);
		builder.Property(x => x.Ignored).HasConversion(JsonValueConverter<List<string>>.Instance);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="CustomFormat" />.</summary>
internal sealed class CustomFormatConfiguration : EntityConfiguration<CustomFormat>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<CustomFormat> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.Specifications).HasConversion(JsonValueConverter<List<CustomFormatSpecification>>.Instance);
	}
}

/// <summary>Configuration for <see cref="ReleaseFilter" />.</summary>
internal sealed class ReleaseFilterConfiguration : EntityConfiguration<ReleaseFilter>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<ReleaseFilter> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Values).HasConversion(JsonValueConverter<List<string>>.Instance);
	}
}

/// <summary>Configuration for <see cref="ReleaseGroupQualityOverride" />.</summary>
internal sealed class ReleaseGroupQualityOverrideConfiguration : EntityConfiguration<ReleaseGroupQualityOverride>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<ReleaseGroupQualityOverride> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.ReleaseGroup).IsUnique();
		builder.Property(x => x.ReleaseGroup).HasMaxLength(256);
	}
}
