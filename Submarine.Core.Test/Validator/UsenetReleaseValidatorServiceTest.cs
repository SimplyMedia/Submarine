using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Submarine.Core.Release.Exceptions;
using Submarine.Core.Validator;
using Xunit;
using Xunit.Abstractions;

namespace Submarine.Core.Test.Validator;

public class UsenetReleaseValidatorServiceTest
{
	private readonly IValidator<string> _instance;

	public UsenetReleaseValidatorServiceTest(ITestOutputHelper output)
		=> _instance = new UsenetReleaseValidatorService(new XunitLogger<UsenetReleaseValidatorService>(output));

	[Theory]
	[InlineData("76El6LcgLzqb426WoVFg1vVVVGx4uCYopQkfjmLe")]
	[InlineData("TDAsqTea7k4o6iofVx3MQGuDK116FSjPobMuh8oB")]
	[InlineData("yp4nFodAAzoeoRc467HRh1mzuT17qeekmuJ3zFnL")]
	[InlineData("dPBAtu681Ycy3A4NpJDH6kNVQooLxqtnsW1Umfiv")]
	[InlineData("185d86a343e39f3341e35c4dad3f9959")]
	[InlineData("ba27283b17c00d01193eacc02a8ba98eeb523a76")]
	[InlineData("45a55debe3856da318cc35882ad07e43cd32fd15")]
	[InlineData("86420f8ee425340d8894bf3bc636b66404b95f18")]
	[InlineData("ce39afb7da6cf7c04eba3090f0a309f609883862")]
	[InlineData("Vh1FvU3bJXw6zs8EEUX4bMo5vbbMdHghxHirc.mkv")]
	[InlineData("0e895c37245186812cb08aab1529cf8ee389dd05.mkv")]
	[InlineData("08bbc153931ce3ca5fcafe1b92d3297285feb061.mkv")]
	[InlineData("185d86a343e39f3341e35c4dad3ff159")]
	[InlineData("ah63jka93jf0jh26ahjas961.mkv")]
	[InlineData("QZC4HDl7ncmzyUj9amucWe1ddKU1oFMZDd8r0dEDUsTd")]
	[InlineData("e096aeb3c2c0483a96f5b32fc6d10ff5")]
	public void Validate_ShouldThrow_WhenReleaseIsHashed(string input)
		=> AssertThrows(input);

	[Theory]
	[InlineData("password - \"bdc435cb-93c4-4902-97ea-ca00568c3887.337\" yEnc")]
	public void Validate_ShouldThrow_WhenReleaseIsPassworded(string input)
		=> AssertThrows(input);

	[Theory]
	[MemberData(nameof(GenerateMD5))]
	public void Validate_ShouldThrow_WhenReleaseIsMD5(string input)
		=> AssertThrows(input);

	[Theory]
	[MemberData(nameof(GenerateRandom), 32)]
	[MemberData(nameof(GenerateRandom), 40)]
	public void Validate_ShouldThrow_WhenReleaseIsRandom(string input)
		=> AssertThrows(input);

	[Theory]
	[InlineData("   ")]
	[InlineData("#######")]
	[InlineData("____.-.-.-.-.-.:-...--..--.-")]
	public void Validate_ShouldThrow_WhenNoCharOrDigitInRelease(string input)
		=> AssertThrows(input);

	[Theory]
	[InlineData("Movie.Title.The.Actual.Title.2003.UHD.BluRay.2160p.TrueHD.Atmos.7.1.DV.HEVC.HYBRID.REMUX-FraMeSToR")]
	public void Validate_ShouldNotThrow_WhenReleaseIsValid(string input)
	{
		var exception = Record.Exception(() => _instance.Validate(input));
		
		Assert.Null(exception);
	}
	
	public static IEnumerable<object[]> GenerateRandom(int length)
	{
		const string charset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

		var random = new Random();

		const int amount = 100;

		var test = Enumerable.Range(0, amount)
			.Select(_ =>
			{
				var builder = new StringBuilder(length);

				for (var x = 0; x < length; x++) builder.Append(charset[random.Next() % charset.Length]);

				return new[] { (object)builder.ToString() };
			}).ToArray();

		return test;
	}

	public static IEnumerable<object[]> GenerateMD5()
	{
		var hash = "Submarine Test Seed";

		var hashAlgo = MD5.Create();

		const int amount = 100;

		var test = Enumerable.Range(0, amount)
			.Select(_ =>
			{
				var hashData = hashAlgo.ComputeHash(Encoding.Default.GetBytes(hash));

				hash = BitConverter.ToString(hashData).Replace("-", "");

				return new[] { (object)hash };
			}).ToArray();

		return test;
	}

	private void AssertThrows(string input)
		=> Assert.Throws<InvalidReleaseException>(() => _instance.Validate(input));
}
