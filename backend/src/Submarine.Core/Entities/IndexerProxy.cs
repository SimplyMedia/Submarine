using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     Outbound proxy configuration for indexer requests.
/// </summary>
public sealed class IndexerProxy : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Proxy type.</summary>
	public IndexerProxyType Type { get; set; }

	/// <summary>Proxy host.</summary>
	public string Host { get; set; } = string.Empty;

	/// <summary>Proxy port.</summary>
	public int Port { get; set; }

	/// <summary>Username.</summary>
	public string? Username { get; set; }

	/// <summary>Password.</summary>
	public string? Password { get; set; }

	/// <summary>Request timeout in seconds.</summary>
	public int RequestTimeoutSeconds { get; set; } = 100;

	/// <summary>Tags this proxy applies to.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
