using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Config;

/// <summary>
///     Configuration for how Indexers are queried
/// </summary>
public class IndexerConfig : IUpdatable
{
	/// <summary>
	///     Id of the indexer config, this is a singleton entity with a fixed Id of 1
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; set; }

	/// <summary>
	///     Interval in minutes between RSS sync runs, 0 disables RSS sync
	/// </summary>
	public int RssSyncIntervalMinutes { get; set; } = 30;

	/// <summary>
	///     Minimum age in minutes a release must have before it is grabbed (usenet only)
	/// </summary>
	public int MinimumAgeMinutes { get; set; }

	/// <summary>
	///     Maximum age in days of releases to consider, 0 means unlimited
	/// </summary>
	public int RetentionDays { get; set; }

	/// <summary>
	///     Maximum size in megabytes of releases to consider, 0 means unlimited
	/// </summary>
	public int MaximumSizeMb { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
