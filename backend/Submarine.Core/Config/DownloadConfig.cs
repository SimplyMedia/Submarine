using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Config;

/// <summary>
///     Configuration for how downloads are handled
/// </summary>
public class DownloadConfig : IUpdatable
{
	/// <summary>
	///     Id of the download config, this is a singleton entity with a fixed Id of 1
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; set; }

	/// <summary>
	///     Whether failed downloads should be handled automatically
	/// </summary>
	public bool EnableFailedDownloadHandling { get; set; } = true;

	/// <summary>
	///     Whether a different release should be grabbed when a download fails
	/// </summary>
	public bool RedownloadFailedReleases { get; set; } = true;

	/// <summary>
	///     Whether failed downloads should be removed from the download client
	/// </summary>
	public bool RemoveFailedFromClient { get; set; } = true;

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
