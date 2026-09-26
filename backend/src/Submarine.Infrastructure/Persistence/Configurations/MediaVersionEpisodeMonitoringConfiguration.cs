using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

internal sealed class MediaVersionEpisodeMonitoringConfiguration : EntityConfiguration<MediaVersionEpisodeMonitoring>
{
	public override void Configure(EntityTypeBuilder<MediaVersionEpisodeMonitoring> builder)
	{
		base.Configure(builder);
		builder.ToTable("MediaVersionEpisodeMonitorings");
		builder.HasIndex(x => new { x.MediaVersionId, x.EpisodeId }).IsUnique();
		builder.HasOne(x => x.MediaVersion).WithMany().HasForeignKey(x => x.MediaVersionId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(x => x.Episode).WithMany().HasForeignKey(x => x.EpisodeId).OnDelete(DeleteBehavior.Cascade);
	}
}
