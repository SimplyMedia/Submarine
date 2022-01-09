namespace Submarine.Core.Util.Extensions;

public static class StringExtensions
{
	public static string Reverse(this string str)
	{
		var chars = new char[str.Length];
		for (int i = 0, j = str.Length - 1; i <= j; i++, j--)
		{
			chars[i] = str[j];
			chars[j] = str[i];
		}

		return new string(chars);
	}
	}
}
