using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Provider;

namespace Submarine.Core.Profile;

/// <summary>
///     A Delay Profile controls how long grabbing a Release is delayed to allow a better Release to appear
/// </summary>
public class DelayProfile : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the delay profile
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the delay profile
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Protocol preferred by this profile when a Release is available on both Protocols
	/// </summary>
	public Protocol PreferredProtocol { get; set; }

	/// <summary>
	///     Minutes a Usenet Release is delayed before it is grabbed
	/// </summary>
	public int UsenetDelayMinutes { get; set; }

	/// <summary>
	///     Minutes a Torrent Release is delayed before it is grabbed
	/// </summary>
	public int TorrentDelayMinutes { get; set; }

	/// <summary>
	///     Whether the delay is bypassed once a Release already meets the highest wanted Quality
	/// </summary>
	public bool BypassIfHighestQuality { get; set; }

	/// <summary>
	///     Order in which profiles are evaluated against a Series or Movie's Tags, lower is evaluated first
	/// </summary>
	public int Order { get; set; }

	/// <summary>
	///     Tags a Series or Movie must have for this profile to apply
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
