using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configuration for <see cref="User" />.
/// </summary>
internal sealed class UserConfiguration : EntityConfiguration<User>
{
	/// <inheritdoc />
	public override void Configure(EntityTypeBuilder<User> builder)
	{
		base.Configure(builder);
		builder.HasIndex(x => x.Username).IsUnique();
		builder.Property(x => x.Username).HasMaxLength(256);
		builder.Property(x => x.PasswordHash).HasMaxLength(1024);
	}
}
