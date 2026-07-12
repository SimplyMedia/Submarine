using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Notification;

/// <summary>
///     A Connection to a media server which is notified about library changes
/// </summary>
public class Connection : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the connection
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the connection
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Type of media server this connection talks to
	/// </summary>
	public ConnectionType Type { get; set; }

	/// <summary>
	///     Whether this connection is enabled
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	///     Host of the media server
	/// </summary>
	public string Host { get; set; }

	/// <summary>
	///     Port of the media server
	/// </summary>
	public int Port { get; set; }

	/// <summary>
	///     Whether to connect via https
	/// </summary>
	public bool UseSsl { get; set; }

	/// <summary>
	///     Api key or token used to authenticate against the media server
	/// </summary>
	public string ApiKey { get; set; }

	/// <summary>
	///     Whether this connection is notified when a release is grabbed
	/// </summary>
	public bool OnGrab { get; set; }

	/// <summary>
	///     Whether this connection is notified when media is imported
	/// </summary>
	public bool OnImport { get; set; }

	/// <summary>
	///     Whether this connection is notified when media files are renamed
	/// </summary>
	public bool OnRename { get; set; }

	/// <summary>
	///     Tags of this connection, empty means it applies to all media
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
