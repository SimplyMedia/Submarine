using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

internal sealed class CompatLibraryBindingConfiguration : EntityConfiguration<CompatLibraryBinding>
{
	public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<CompatLibraryBinding> builder)
	{
		base.Configure(builder);
		builder.ToTable("CompatLibraryBindings", table => table.HasCheckConstraint(
			"CK_CompatLibraryBindings_FacadeTitle",
			"(\"Facade\" = 'sonarr' AND \"SeriesId\" IS NOT NULL AND \"MovieId\" IS NULL) OR (\"Facade\" = 'radarr' AND \"SeriesId\" IS NULL AND \"MovieId\" IS NOT NULL)"));
		builder.Property(x => x.Facade).HasMaxLength(16).IsRequired();
		builder.HasIndex(x => new { x.Facade, x.SeriesId }).IsUnique().HasFilter("\"SeriesId\" IS NOT NULL");
		builder.HasIndex(x => new { x.Facade, x.MovieId }).IsUnique().HasFilter("\"MovieId\" IS NOT NULL");
		builder.HasIndex(x => new { x.Facade, x.MediaVersionId }).IsUnique().HasFilter("\"MediaVersionId\" IS NOT NULL");
		builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(x => x.MediaVersion).WithMany().HasForeignKey(x => x.MediaVersionId).OnDelete(DeleteBehavior.Restrict);
	}
}
