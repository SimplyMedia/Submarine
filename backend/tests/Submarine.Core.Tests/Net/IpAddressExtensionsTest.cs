using System.Net;
using Shouldly;
using Submarine.Core.Net;
using Xunit;

namespace Submarine.Core.Tests.Net;

public sealed class IpAddressExtensionsTest
{
	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("::1")]
	[InlineData("10.64.5.1")]
	[InlineData("172.16.0.1")]
	[InlineData("172.31.255.255")]
	[InlineData("192.168.5.1")]
	[InlineData("169.254.1.1")]
	[InlineData("fe80::1")]
	[InlineData("fd00::1")]
	public void IsLocalAddress_ShouldBeTrue_ForLocalAddresses(string address)
		=> IPAddress.Parse(address).IsLocalAddress().ShouldBeTrue();

	[Theory]
	[InlineData("1.2.3.4")]
	[InlineData("172.15.255.255")]
	[InlineData("172.32.0.0")]
	[InlineData("192.169.0.1")]
	[InlineData("8.8.8.8")]
	public void IsLocalAddress_ShouldBeFalse_ForPublicAddresses(string address)
		=> IPAddress.Parse(address).IsLocalAddress().ShouldBeFalse();

	[Fact]
	public void IsLocalAddress_ShouldUnmapIPv4MappedIPv6_BeforeClassifying()
		=> IPAddress.Parse("::ffff:10.0.0.1").IsLocalAddress().ShouldBeTrue();
}
