using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Library;

namespace Submarine.Core.ImportList;

/// <summary>
///     An Import List periodically adds media from an external source to the library
/// </summary>
public class ImportList : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the import list
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the import list
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Source type of the import list
	/// </summary>
	public ImportListType Type { get; set; }

	/// <summary>
	///     Whether this import list is enabled
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	///     The type specific settings of this import list (list id, username, season parameters), serialized as JSON
	/// </summary>
	public string SettingsJson { get; set; }

	/// <summary>
	///     Kind of media this import list adds
	/// </summary>
	public MediaKind MediaKind { get; set; }

	/// <summary>
	///     Id of the Quality Profile assigned to added media
	/// </summary>
	public int QualityProfileId { get; set; }

	/// <summary>
	///     Id of the Language Profile assigned to added media
	/// </summary>
	public int LanguageProfileId { get; set; }

	/// <summary>
	///     Id of the Root Folder added media is stored under
	/// </summary>
	public int RootFolderId { get; set; }

	/// <summary>
	///     Whether added media is monitored
	/// </summary>
	public bool Monitored { get; set; } = true;

	/// <summary>
	///     Tags applied to added media
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
