using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

internal sealed class MediaVersionSeasonMonitoringConfiguration : EntityConfiguration<MediaVersionSeasonMonitoring>
{
	public override void Configure(EntityTypeBuilder<MediaVersionSeasonMonitoring> builder)
	{
		base.Configure(builder);
		builder.ToTable("MediaVersionSeasonMonitorings");
		builder.HasIndex(x => new { x.MediaVersionId, x.SeasonNumber }).IsUnique();
		builder.HasOne(x => x.MediaVersion).WithMany().HasForeignKey(x => x.MediaVersionId).OnDelete(DeleteBehavior.Cascade);
	}
}
