using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Library;

/// <summary>
///     A Root Folder is a directory on disk under which Media of a certain <see cref="Library.MediaKind" /> is stored
/// </summary>
public class RootFolder : ICreatable
{
	/// <summary>
	///     Id of the root folder
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Path of this root folder on disk, must be unique
	/// </summary>
	public string Path { get; set; } = string.Empty;

	/// <summary>
	///     Kind of media stored under this root folder
	/// </summary>
	public MediaKind MediaKind { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }
}
