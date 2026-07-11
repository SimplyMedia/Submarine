using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Provider;

/// <summary>
///     A Provider is a Service which offers you a way of obtaining files
/// </summary>
public abstract class Provider : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the provider
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the provider
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Protocol which the provider uses
	/// </summary>
	public Protocol Protocol { get; set; }

	/// <summary>
	///     Download mode this provider should be used for
	/// </summary>
	public ProviderMode Mode { get; set; }

	/// <summary>
	///     Url of this provider
	/// </summary>
	public string Url { get; set; }

	/// <summary>
	///     ApiKey of this provider
	/// </summary>
	public string ApiKey { get; set; }

	/// <summary>
	///     Priority of this provider
	/// </summary>
	public short Priority { get; set; }

	/// <summary>
	///     Tags of this provider
	/// </summary>
	public List<string> Tags { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
