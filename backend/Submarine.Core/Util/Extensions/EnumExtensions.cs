using System;
using System.Reflection;

namespace Submarine.Core.Util.Extensions;

/// <summary>
///     Extensions for Enums
/// </summary>
public static class EnumExtensions
{
	/// <summary>
	///     Get the Attribute of an Enum
	/// </summary>
	/// <param name="value">The enum to get the Attribute for</param>
	/// <typeparam name="TAttribute">The Attribute to get</typeparam>
	/// <returns>The existing Attribute for that enum member, if any</returns>
	public static TAttribute? GetAttribute<TAttribute>(this Enum value)
		where TAttribute : Attribute
	{
		var enumType = value.GetType();
		var name = Enum.GetName(enumType, value);

		if (name == null)
			return null;

		var field = enumType.GetField(name);
		return field == null ? null : field.GetCustomAttribute<TAttribute>(false);
	}
}
