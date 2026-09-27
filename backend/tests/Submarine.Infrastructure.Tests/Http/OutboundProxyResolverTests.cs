using System.Net;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Http;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Http;

public sealed class OutboundProxyResolverTests
{
	[Fact]
	public void ApplyProxy_ShouldDisableSystemProxy_WhenUsingSocks()
	{
		using var handler = new SocketsHttpHandler();
		var snapshot = new OutboundProxySnapshot(
			true, IndexerProxyType.SOCKS5, "proxy.example", 1080, null, null, "", true, CertificateValidationType.ENABLED);

		OutboundProxyResolver.ApplyProxy(handler, snapshot);

		handler.UseProxy.ShouldBeFalse();
		handler.ConnectCallback.ShouldNotBeNull();
	}

	[Fact]
	public void ShouldBypass_ShouldHonorWildcardFilters()
	{
		var snapshot = new OutboundProxySnapshot(
			true, IndexerProxyType.SOCKS5, "proxy.example", 1080, null, null, "*.example.com", false, CertificateValidationType.ENABLED);

		OutboundProxyResolver.ShouldBypass(snapshot, new Uri("http://indexer.example.com")).ShouldBeTrue();
		OutboundProxyResolver.ShouldBypass(snapshot, new Uri("http://elsewhere.test")).ShouldBeFalse();
	}
}
