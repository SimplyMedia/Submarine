using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Download;

/// <summary>
///     Persisted configuration of a Download Client, storing its type specific settings as JSON
/// </summary>
public class DownloadClientConfig : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the download client
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the download client
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Type of the download client
	/// </summary>
	public DownloadClientType Type { get; set; }

	/// <summary>
	///     Whether this download client is enabled
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	///     Priority of this download client, lower is preferred
	/// </summary>
	public int Priority { get; set; } = 1;

	/// <summary>
	///     The type specific settings record of this download client, serialized as JSON
	/// </summary>
	public string SettingsJson { get; set; }

	/// <summary>
	///     Tags of this download client
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
