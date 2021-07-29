using System;
using System.Collections.Generic;
using System.Linq;

namespace Submarine.Core.Util
{
	public class EnumUtil
	{
		public static IEnumerable<T> GetEnumValues<T>()
		{
			// Can't use type constraints on value types, so have to do check like this
			if (typeof(T).BaseType != typeof(Enum))
			{
				throw new ArgumentException("T must be of type System.Enum");
			}

			return Enum.GetValues(typeof(T)).Cast<T>();
		}
	}
}
