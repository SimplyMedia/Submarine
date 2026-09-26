using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence.Converters;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="Tag" />.</summary>
internal sealed class TagConfiguration : EntityConfiguration<Tag>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Tag> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.Label).IsUnique();
		builder.Property(x => x.Label).HasMaxLength(256);
	}
}

/// <summary>Configuration for <see cref="RootFolder" />.</summary>
internal sealed class RootFolderConfiguration : EntityConfiguration<RootFolder>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<RootFolder> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.Path).IsUnique();
		builder.Property(x => x.Path).HasMaxLength(1024);
	}
}

/// <summary>Configuration for <see cref="Series" />.</summary>
internal sealed class SeriesConfiguration : EntityConfiguration<Series>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Series> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.TvdbId).IsUnique();
		builder.Property(x => x.Title).HasMaxLength(512);
		builder.Property(x => x.ImdbId).HasMaxLength(32);
		builder.Property(x => x.Genres).HasConversion(JsonValueConverter<List<string>>.Instance);
		builder.HasMany(x => x.Seasons).WithOne(x => x.Series).HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
		builder.HasMany(x => x.Episodes).WithOne(x => x.Series).HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
		builder.HasMany(x => x.Versions).WithOne(x => x.Series).HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="Season" />.</summary>
internal sealed class SeasonConfiguration : EntityConfiguration<Season>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Season> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => new { x.SeriesId, x.SeasonNumber }).IsUnique();
	}
}

/// <summary>Configuration for <see cref="Episode" />.</summary>
internal sealed class EpisodeConfiguration : EntityConfiguration<Episode>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Episode> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => new { x.SeriesId, x.SeasonNumber, x.EpisodeNumber }).IsUnique();
		builder.Property(x => x.AirDate).HasMaxLength(10);
		builder.HasMany(x => x.Files).WithMany(x => x.Episodes).UsingEntity("EpisodeFileEpisode");
	}
}

/// <summary>Configuration for <see cref="Movie" />.</summary>
internal sealed class MovieConfiguration : EntityConfiguration<Movie>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Movie> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.TmdbId).IsUnique();
		builder.Property(x => x.Title).HasMaxLength(512);
		builder.Property(x => x.ImdbId).HasMaxLength(32);
		builder.Property(x => x.Genres).HasConversion(JsonValueConverter<List<string>>.Instance);
		builder.Property(x => x.Keywords).HasConversion(JsonValueConverter<List<string>>.Instance);
		builder.HasMany(x => x.Versions).WithOne(x => x.Movie).HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
		builder.HasMany(x => x.Files).WithOne(x => x.Movie).HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
		builder.HasMany(x => x.Tags).WithMany();
	}
}

/// <summary>Configuration for <see cref="Collection" />.</summary>
internal sealed class CollectionConfiguration : EntityConfiguration<Collection>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Collection> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.TmdbCollectionId).IsUnique();
		builder.Property(x => x.Title).HasMaxLength(512);
	}
}

/// <summary>Configuration for <see cref="MediaVersion" />.</summary>
internal sealed class MediaVersionConfiguration : EntityConfiguration<MediaVersion>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<MediaVersion> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		builder.Property(x => x.Path).HasMaxLength(1024);
		builder.HasOne(x => x.Series).WithMany(x => x.Versions).HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(x => x.Movie).WithMany(x => x.Versions).HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>Configuration for <see cref="EpisodeFile" />.</summary>
internal sealed class EpisodeFileConfiguration : EntityConfiguration<EpisodeFile>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<EpisodeFile> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.RelativePath).HasMaxLength(1024);
		builder.Property(x => x.Quality).HasConversion(JsonValueConverter<QualityModel>.Instance);
		builder.Property(x => x.Languages).HasConversion(JsonValueConverter<List<Language>>.Instance);
		builder.Property(x => x.MediaInfo).HasConversion(JsonValueConverter<MediaInfoModel?>.Instance);
		builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(x => x.MediaVersion).WithMany(x => x.EpisodeFiles).HasForeignKey(x => x.MediaVersionId).OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>Configuration for <see cref="MovieFile" />.</summary>
internal sealed class MovieFileConfiguration : EntityConfiguration<MovieFile>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<MovieFile> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.RelativePath).HasMaxLength(1024);
		builder.Property(x => x.Quality).HasConversion(JsonValueConverter<QualityModel>.Instance);
		builder.Property(x => x.Languages).HasConversion(JsonValueConverter<List<Language>>.Instance);
		builder.Property(x => x.MediaInfo).HasConversion(JsonValueConverter<MediaInfoModel?>.Instance);
		builder.HasOne(x => x.Movie).WithMany(x => x.Files).HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(x => x.MediaVersion).WithMany(x => x.MovieFiles).HasForeignKey(x => x.MediaVersionId).OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>Configuration for <see cref="AlternativeTitle" />.</summary>
internal sealed class AlternativeTitleConfiguration : EntityConfiguration<AlternativeTitle>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<AlternativeTitle> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Title).HasMaxLength(512);
		builder.HasOne<Series>().WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne<Movie>().WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
	}
}
