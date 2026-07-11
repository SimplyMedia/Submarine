using System;

namespace Submarine.Core.Quality.Attributes;

/// <summary>
///     Attribute to specify the display name of an enum value
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class DisplayNameAttribute : Attribute
{
	/// <summary>
	///     The display name of the enum value
	/// </summary>
	public string Name { get; }

	/// <summary>
	///     Creates a new instance of <see cref="DisplayNameAttribute" />
	/// </summary>
	/// <param name="name">The display name of the enum value</param>
	public DisplayNameAttribute(string name)
		=> Name = name;
}
