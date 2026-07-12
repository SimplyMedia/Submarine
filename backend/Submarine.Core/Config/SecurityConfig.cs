using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Config;

/// <summary>
///     Configuration for API authentication
/// </summary>
public class SecurityConfig : IUpdatable
{
	/// <summary>
	///     Id of the security config, this is a singleton entity with a fixed Id of 1
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; set; }

	/// <summary>
	///     Authentication method required for API requests
	/// </summary>
	public AuthenticationMethod Method { get; set; } = AuthenticationMethod.API_KEY;

	/// <summary>
	///     API key required when <see cref="Method" /> is <see cref="AuthenticationMethod.API_KEY" />
	/// </summary>
	public string ApiKey { get; set; }

	/// <summary>
	///     Token used to authenticate calendar/RSS feed URLs without requiring the API key
	/// </summary>
	public string FeedToken { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
