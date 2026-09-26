using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence.Converters;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="Indexer" />.</summary>
internal sealed class IndexerConfiguration : EntityConfiguration<Indexer>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Indexer> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.BaseUrl).HasMaxLength(1024);
		builder.Property(x => x.Categories).HasConversion(JsonValueConverter<List<int>>.Instance);
		builder.Property(x => x.AnimeCategories).HasConversion(JsonValueConverter<List<int>>.Instance);
		builder.HasOne(x => x.DownloadClient).WithMany().HasForeignKey(x => x.DownloadClientId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Proxy).WithMany().HasForeignKey(x => x.ProxyId).OnDelete(DeleteBehavior.SetNull);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="IndexerDefinition" />.</summary>
internal sealed class IndexerDefinitionConfiguration : EntityConfiguration<IndexerDefinition>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<IndexerDefinition> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.DefinitionId).IsUnique();
		builder.Property(x => x.DefinitionId).HasMaxLength(256);
		builder.Property(x => x.Version).HasMaxLength(64);
		builder.Property(x => x.Links).HasConversion(JsonValueConverter<List<string>>.Instance);
	}
}

/// <summary>Configuration for <see cref="IndexerProxy" />.</summary>
internal sealed class IndexerProxyConfiguration : EntityConfiguration<IndexerProxy>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<IndexerProxy> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.Host).HasMaxLength(512);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="IndexerStatus" />.</summary>
internal sealed class IndexerStatusConfiguration : EntityConfiguration<IndexerStatus>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<IndexerStatus> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.IndexerId).IsUnique();
		builder.HasOne(x => x.Indexer).WithMany().HasForeignKey(x => x.IndexerId).OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>Configuration for <see cref="IndexerHistory" />.</summary>
internal sealed class IndexerHistoryConfiguration : EntityConfiguration<IndexerHistory>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<IndexerHistory> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.Date);
		builder.HasOne(x => x.Indexer).WithMany().HasForeignKey(x => x.IndexerId).OnDelete(DeleteBehavior.Cascade);
	}
}
