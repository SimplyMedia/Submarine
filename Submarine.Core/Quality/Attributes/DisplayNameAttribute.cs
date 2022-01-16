using System;

namespace Submarine.Core.Quality.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class DisplayNameAttribute : Attribute
{
	public string Name { get; }

	public DisplayNameAttribute(string name)
		=> Name = name;
}
