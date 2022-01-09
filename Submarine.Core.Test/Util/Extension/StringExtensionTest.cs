using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Submarine.Core.Util.Extensions;
using Xunit;

namespace Submarine.Core.Test.Util.Extension;

public class StringExtensionTest
{
	[Theory]
	[MemberData(nameof(GenerateRandomStrings))]
	public void Reverse_ShouldReturnReversedString_WhenProvided(string input)
	{
		var charArray = input.ToCharArray();
		Array.Reverse(charArray);
		var reversed = new string(charArray);

		Assert.Equal(reversed, input.Reverse());
	}

	public static IEnumerable<object[]> GenerateRandomStrings()
	{
		const string charset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

		var random = new Random();

		const int amount = 100;

		const int length = 30;

		var test = Enumerable.Range(0, amount)
			.Select(_ =>
			{
				var builder = new StringBuilder(length);

				for (var x = 0; x < length; x++) builder.Append(charset[random.Next() % charset.Length]);

				return new[] { (object)builder.ToString() };
			}).ToArray();

		return test;
	}
}
