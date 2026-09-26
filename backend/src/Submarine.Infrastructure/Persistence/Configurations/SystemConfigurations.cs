using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Commands;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>Configuration for <see cref="Command" />.</summary>
internal sealed class CommandConfiguration : EntityConfiguration<Command>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<Command> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Name).HasMaxLength(256);
		// At most one Queued or Running row per (Name, BodyHash): dedupe is enforced by this index,
		// not just the read-then-insert check in CommandQueue.EnqueueAsync.
		builder.HasIndex(x => new { x.Name, x.BodyHash })
			.IsUnique()
			.HasFilter($"\"{nameof(Command.Status)}\" IN ({(int)CommandStatus.QUEUED}, {(int)CommandStatus.RUNNING})");
		builder.HasIndex(x => x.Status);
	}
}

/// <summary>Configuration for <see cref="ScheduledTask" />.</summary>
internal sealed class ScheduledTaskConfiguration : EntityConfiguration<ScheduledTask>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<ScheduledTask> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.Name).IsUnique();
		builder.Property(x => x.Name).HasMaxLength(256);
	}
}

/// <summary>Configuration for <see cref="HealthIssue" />.</summary>
internal sealed class HealthIssueConfiguration : EntityConfiguration<HealthIssue>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<HealthIssue> builder)
	{
		base.Configure(builder);
		builder.Property(x => x.Source).HasMaxLength(256);
		builder.Property(x => x.WikiUrl).HasMaxLength(1024);
	}
}
