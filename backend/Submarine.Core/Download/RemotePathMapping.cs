using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Download;

/// <summary>
///     Maps a path as reported by a remote Download Client to a local path Submarine can access, e.g. when the
///     Download Client runs on a different host or inside a container
/// </summary>
public class RemotePathMapping : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the remote path mapping
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Host of the Download Client this mapping applies to
	/// </summary>
	public string Host { get; set; } = string.Empty;

	/// <summary>
	///     Path as reported by the remote Download Client
	/// </summary>
	public string RemotePath { get; set; } = string.Empty;

	/// <summary>
	///     Local path the remote path is mapped to
	/// </summary>
	public string LocalPath { get; set; } = string.Empty;

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
