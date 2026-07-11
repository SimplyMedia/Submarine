using System;

namespace Submarine.Core.Provider;

/// <summary>
///     Mode in which a <see cref="Provider" /> can run
/// </summary>
[Flags]
public enum ProviderMode
{
	/// <summary>
	///     No mode is active, Provider is not enabled
	/// </summary>
	NONE = 0,

	/// <summary>
	///     RSS Mode, it will periodically be requested for latest releases
	/// </summary>
	RSS = 1 << 1,

	/// <summary>
	///     Automatic Search, it will be requested when an Automatic search is issued
	/// </summary>
	AUTOMATIC_SEARCH = 1 << 2,

	/// <summary>
	///     Manual Search, it will be requested when a Manual search is issued
	/// </summary>
	MANUAL_SEARCH = 1 << 3
}
