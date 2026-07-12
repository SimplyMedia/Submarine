using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Provider;

namespace Submarine.Core.Download;

/// <summary>
///     A release which failed and should not be grabbed again
/// </summary>
public class BlocklistItem : ICreatable
{
	/// <summary>
	///     Id of the blocklist item
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Title of the blocked release
	/// </summary>
	public string ReleaseTitle { get; set; } = string.Empty;

	/// <summary>
	///     Indexer-side identifier of the blocked release, if any
	/// </summary>
	public string? Guid { get; set; }

	/// <summary>
	///     Protocol the blocked release was grabbed over
	/// </summary>
	public Protocol Protocol { get; set; }

	/// <summary>
	///     Name of the indexer the blocked release originates from, if any
	/// </summary>
	public string? Indexer { get; set; }

	/// <summary>
	///     Id of the Series the blocked release was for, if any
	/// </summary>
	public int? SeriesId { get; set; }

	/// <summary>
	///     Id of the Movie the blocked release was for, if any
	/// </summary>
	public int? MovieId { get; set; }

	/// <summary>
	///     Why this release was blocked
	/// </summary>
	public string Reason { get; set; } = string.Empty;

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }
}
