using System;

namespace Submarine.Core.Quality.Attributes
{
	[AttributeUsage(AttributeTargets.Field)]
	public class NameAttribute : Attribute
	{
		public string Name { get; }

		public NameAttribute(string name) 
			=> Name = name;
	}
}