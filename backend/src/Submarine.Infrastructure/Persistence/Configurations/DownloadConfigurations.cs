using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence.Converters;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="DownloadClient" />.</summary>
internal sealed class DownloadClientConfiguration : EntityConfiguration<DownloadClient>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<DownloadClient> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.RemoveCompleted).HasDefaultValue(true);
		builder.Property(x => x.RemoveFailed).HasDefaultValue(true);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="RemotePathMapping" />.</summary>
internal sealed class RemotePathMappingConfiguration : EntityConfiguration<RemotePathMapping>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<RemotePathMapping> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Host).HasMaxLength(512);
		builder.Property(x => x.RemotePath).HasMaxLength(1024);
		builder.Property(x => x.LocalPath).HasMaxLength(1024);
	}
}

/// <summary>Configuration for <see cref="TrackedDownload" />.</summary>
internal sealed class TrackedDownloadConfiguration : EntityConfiguration<TrackedDownload>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<TrackedDownload> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => new { x.DownloadClientId, x.DownloadId }).IsUnique();
		builder.Property(x => x.Quality).HasConversion(JsonValueConverter<QualityModel?>.Instance);
		builder.Property(x => x.Languages).HasConversion(JsonValueConverter<List<Language>>.Instance);
		builder.Property(x => x.EpisodeIds).HasConversion(JsonValueConverter<List<int>>.Instance);
		builder.Property(x => x.StatusMessages).HasConversion(JsonValueConverter<List<string>>.Instance);
		builder.HasOne(x => x.DownloadClient).WithMany().HasForeignKey(x => x.DownloadClientId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.MediaVersion).WithMany().HasForeignKey(x => x.MediaVersionId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Indexer).WithMany().HasForeignKey(x => x.IndexerId).OnDelete(DeleteBehavior.SetNull);
	}
}

/// <summary>Configuration for <see cref="HistoryEvent" />.</summary>
internal sealed class HistoryEventConfiguration : EntityConfiguration<HistoryEvent>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<HistoryEvent> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.Date);
		builder.HasIndex(x => x.DownloadId);
		builder.Property(x => x.Quality).HasConversion(JsonValueConverter<QualityModel?>.Instance);
		builder.Property(x => x.Languages).HasConversion(JsonValueConverter<List<Language>?>.Instance);
		builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Episode).WithMany().HasForeignKey(x => x.EpisodeId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.MediaVersion).WithMany().HasForeignKey(x => x.MediaVersionId).OnDelete(DeleteBehavior.SetNull);
	}
}

/// <summary>Configuration for <see cref="BlocklistItem" />.</summary>
internal sealed class BlocklistItemConfiguration : EntityConfiguration<BlocklistItem>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<BlocklistItem> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.EpisodeIds).HasConversion(JsonValueConverter<List<int>>.Instance);
		builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Indexer).WithMany().HasForeignKey(x => x.IndexerId).OnDelete(DeleteBehavior.SetNull);
	}
}

/// <summary>Configuration for <see cref="PendingRelease" />.</summary>
internal sealed class PendingReleaseConfiguration : EntityConfiguration<PendingRelease>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<PendingRelease> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.EpisodeIds).HasConversion(JsonValueConverter<List<int>>.Instance);
		builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.SetNull);
		builder.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.SetNull);
	}
}
