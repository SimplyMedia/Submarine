using System.Collections.Generic;
using Submarine.Core.Download;
using Submarine.Core.Download.Rpc;
using Xunit;

namespace Submarine.Core.Tests.Download.Rpc;

public class XmlRpcClientTest
{
	[Fact]
	public void BuildRequest_ShouldEncodeEveryParamType_WhenGivenMixedValues()
	{
		var xml = XmlRpcClient.BuildRequest("load.raw_start", new object?[]
		{
			"hello", 42, 9_999_999_999L, true, new byte[] { 1, 2, 3 }, new object?[] { "a", "b" }
		});

		Assert.Contains("<methodName>load.raw_start</methodName>", xml);
		Assert.Contains("<string>hello</string>", xml);
		Assert.Contains("<i4>42</i4>", xml);
		Assert.Contains("<i8>9999999999</i8>", xml);
		Assert.Contains("<boolean>1</boolean>", xml);
		Assert.Contains("<base64>AQID</base64>", xml);
		Assert.Contains("<array><data><value><string>a</string></value><value><string>b</string></value></data></array>",
			xml);
	}

	[Fact]
	public void ParseResponse_ShouldReturnStruct_WhenResponseIsStruct()
	{
		const string xml = """
			<methodResponse><params><param><value><struct>
				<member><name>version</name><value><string>0.9.8</string></value></member>
				<member><name>count</name><value><i4>3</i4></value></member>
			</struct></value></param></params></methodResponse>
			""";

		var result = Assert.IsType<Dictionary<string, object?>>(XmlRpcClient.ParseResponse(xml));

		Assert.Equal("0.9.8", result["version"]);
		Assert.Equal(3, result["count"]);
	}

	[Fact]
	public void ParseResponse_ShouldReturnNestedArrays_WhenResponseIsMulticall()
	{
		const string xml = """
			<methodResponse><params><param><value><array><data>
				<value><array><data>
					<value><string>ABCD</string></value>
					<value><i8>1000</i8></value>
				</data></array></value>
			</data></array></value></param></params></methodResponse>
			""";

		var rows = Assert.IsType<List<object?>>(XmlRpcClient.ParseResponse(xml));
		var row = Assert.IsType<List<object?>>(Assert.Single(rows));

		Assert.Equal("ABCD", row[0]);
		Assert.Equal(1000L, row[1]);
	}

	[Fact]
	public void ParseResponse_ShouldDecodeBase64_WhenResponseIsBase64()
	{
		const string xml =
			"<methodResponse><params><param><value><base64>AQID</base64></value></param></params></methodResponse>";

		Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(XmlRpcClient.ParseResponse(xml)));
	}

	[Fact]
	public void ParseResponse_ShouldThrowWithFaultString_WhenResponseIsFault()
	{
		const string xml = """
			<methodResponse><fault><value><struct>
				<member><name>faultCode</name><value><i4>-501</i4></value></member>
				<member><name>faultString</name><value><string>Method not found</string></value></member>
			</struct></value></fault></methodResponse>
			""";

		var exception = Assert.Throws<DownloadClientException>(() => XmlRpcClient.ParseResponse(xml));

		Assert.Contains("Method not found", exception.Message);
	}
}
