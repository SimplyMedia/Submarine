using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="GeneralConfig" />.</summary>
internal sealed class GeneralConfigConfiguration : SingletonEntityConfiguration<GeneralConfig>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<GeneralConfig> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.ApiKey).HasMaxLength(64);
		builder.Property(x => x.FeedToken).HasMaxLength(64);
		builder.Property(x => x.UrlBase).HasMaxLength(512);
		builder.Property(x => x.InstanceName).HasMaxLength(256);
		builder.Property(x => x.LogLevel).HasMaxLength(32);
		builder.Property(x => x.Branch).HasMaxLength(64);
	}
}

/// <summary>Configuration for <see cref="NamingConfig" />.</summary>
internal sealed class NamingConfigConfiguration : SingletonEntityConfiguration<NamingConfig>;

/// <summary>Configuration for <see cref="MediaManagementConfig" />.</summary>
internal sealed class MediaManagementConfigConfiguration : SingletonEntityConfiguration<MediaManagementConfig>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<MediaManagementConfig> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.RecycleBinPath).HasMaxLength(1024);
		builder.Property(x => x.ChmodFolder).HasMaxLength(8);
		builder.Property(x => x.ChmodFile).HasMaxLength(8);
		builder.Property(x => x.ChownGroup).HasMaxLength(128);
	}
}

/// <summary>Configuration for <see cref="IndexerConfig" />.</summary>
internal sealed class IndexerConfigConfiguration : SingletonEntityConfiguration<IndexerConfig>;

/// <summary>Configuration for <see cref="DownloadConfig" />.</summary>
internal sealed class DownloadConfigConfiguration : SingletonEntityConfiguration<DownloadConfig>;

/// <summary>Configuration for <see cref="UiConfig" />.</summary>
internal sealed class UiConfigConfiguration : SingletonEntityConfiguration<UiConfig>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<UiConfig> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.CalendarWeekColumnHeader).HasMaxLength(64);
		builder.Property(x => x.ShortDateFormat).HasMaxLength(64);
		builder.Property(x => x.LongDateFormat).HasMaxLength(128);
		builder.Property(x => x.TimeFormat).HasMaxLength(64);
		builder.Property(x => x.Language).HasMaxLength(16);
	}
}

/// <summary>Configuration for <see cref="ImportListConfig" />.</summary>
internal sealed class ImportListConfigConfiguration : SingletonEntityConfiguration<ImportListConfig>;
