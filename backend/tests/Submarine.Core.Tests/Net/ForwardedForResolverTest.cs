using System.Net;
using Shouldly;
using Submarine.Core.Net;
using Xunit;

namespace Submarine.Core.Tests.Net;

public sealed class ForwardedForResolverTest
{
	[Fact]
	public void Resolve_ShouldIgnoreForwardedFor_FromUntrustedPeer()
	{
		var untrustedPeer = IPAddress.Parse("203.0.113.9");
		var trusted = ForwardedForResolver.ParseNetworks("10.0.0.0/8");

		// A spoofed header claiming a private/local address must not be honoured.
		var resolved = ForwardedForResolver.Resolve(untrustedPeer, ["10.0.0.1"], trusted);

		resolved.ShouldBe(untrustedPeer);
		resolved.IsLocalAddress().ShouldBeFalse();
	}

	[Fact]
	public void Resolve_ShouldHonourForwardedFor_FromTrustedProxy()
	{
		var trustedProxy = IPAddress.Parse("10.0.0.5");
		var realClient = IPAddress.Parse("203.0.113.9");
		var trusted = ForwardedForResolver.ParseNetworks("10.0.0.0/8");

		var resolved = ForwardedForResolver.Resolve(trustedProxy, [realClient.ToString()], trusted);

		resolved.ShouldBe(realClient);
		resolved.IsLocalAddress().ShouldBeFalse();
	}

	[Fact]
	public void Resolve_ShouldStopAtFirstUntrustedHop_InAMultiHopChain()
	{
		var trustedProxy = IPAddress.Parse("10.0.0.5");
		var untrustedIntermediate = IPAddress.Parse("203.0.113.1");
		var claimedClient = IPAddress.Parse("198.51.100.1");
		var trusted = ForwardedForResolver.ParseNetworks("10.0.0.0/8");

		// Chain (client-first): claimedClient, untrustedIntermediate. Only the hop nearest the
		// trusted proxy is peeled since untrustedIntermediate isn't itself trusted.
		var resolved = ForwardedForResolver.Resolve(trustedProxy, [claimedClient.ToString(), untrustedIntermediate.ToString()], trusted);

		resolved.ShouldBe(untrustedIntermediate);
	}

	[Fact]
	public void Resolve_ShouldReturnPeer_WhenNoTrustedNetworksConfigured()
	{
		var peer = IPAddress.Parse("203.0.113.9");

		var resolved = ForwardedForResolver.Resolve(peer, ["10.0.0.1"], []);

		resolved.ShouldBe(peer);
	}

	[Fact]
	public void ParseNetworks_ShouldSkipMalformedEntries()
	{
		var networks = ForwardedForResolver.ParseNetworks("10.0.0.0/8, not-a-cidr, 192.168.0.0/16");

		networks.Count.ShouldBe(2);
	}
}
