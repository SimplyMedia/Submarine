using Submarine.Core.Download;
using Shouldly;
using Xunit;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class XmlRpcSerializerTests
{
	[Fact]
	public void BuildRequest_ShouldSerializeAllValueTypes()
	{
		var request = XmlRpcSerializer.BuildRequest("load.raw_start",
			["", new byte[] { 1, 2, 3 }, "d.custom1.set=tv", true, 42, 9000000000000L,
				new object?[] { "nested", 1 }, new Dictionary<string, object?> { ["key"] = "value" }]);

		request.ShouldStartWith("<?xml version=\"1.0\"?>");
		request.ShouldContain("<methodName>load.raw_start</methodName>");
		request.ShouldContain("<string></string>");
		request.ShouldContain("<base64>AQID</base64>");
		request.ShouldContain("<string>d.custom1.set=tv</string>");
		request.ShouldContain("<boolean>1</boolean>");
		request.ShouldContain("<i4>42</i4>");
		request.ShouldContain("<i8>9000000000000</i8>");
		request.ShouldContain("<array><data><value><string>nested</string></value><value><i4>1</i4></value></data></array>");
		request.ShouldContain("<struct><member><name>key</name><value><string>value</string></value></member></struct>");
	}

	[Fact]
	public void BuildRequest_ShouldEscapeXmlSpecialCharacters()
	{
		var request = XmlRpcSerializer.BuildRequest("load.start", ["magnet:?xt=urn:btih:ab&dn=<a&b>"]);

		request.ShouldContain("magnet:?xt=urn:btih:ab&amp;dn=&lt;a&amp;b&gt;");
	}

	[Fact]
	public void BuildRequest_ShouldSerializeNullAsEmptyString()
	{
		var request = XmlRpcSerializer.BuildRequest("method", [null]);

		request.ShouldContain("<string></string>");
	}

	[Fact]
	public void BuildRequest_ShouldSerializeFalseBool()
	{
		var request = XmlRpcSerializer.BuildRequest("method", [false]);

		request.ShouldContain("<boolean>0</boolean>");
	}

	[Fact]
	public void ParseResponse_ShouldParseScalarsArraysAndStructs()
	{
		const string xml = """
			<?xml version="1.0"?>
			<methodResponse><params><param><value><struct>
				<member><name>version</name><value><string>0.9.8</string></value></member>
				<member><name>rows</name><value><array><data>
					<value><array><data>
						<value><string>HASH</string></value>
						<value><i8>1000</i8></value>
						<value><i4>7</i4></value>
						<value><boolean>1</boolean></value>
						<value><base64>AQID</base64></value>
					</data></array></value>
				</data></array></value></member>
			</struct></value></param></params></methodResponse>
			""";

		var result = XmlRpcSerializer.ParseResponse(xml) as Dictionary<string, object?>;

		result.ShouldNotBeNull();
		result["version"].ShouldBe("0.9.8");
		var rows = result["rows"].ShouldBeOfType<List<object?>>();
		var row = rows[0].ShouldBeOfType<List<object?>>();
		row[0].ShouldBe("HASH");
		row[1].ShouldBe(1000L);
		row[2].ShouldBe(7);
		row[3].ShouldBe(true);
		row[4].ShouldBe(new byte[] { 1, 2, 3 });
	}

	[Fact]
	public void ParseResponse_ShouldThrowWithFaultString_WhenFault()
	{
		const string xml = """
			<?xml version="1.0"?>
			<methodResponse><fault><value><struct>
				<member><name>faultCode</name><value><i4>-501</i4></value></member>
				<member><name>faultString</name><value><string>bad credentials</string></value></member>
			</struct></value></fault></methodResponse>
			""";

		var exception = Should.Throw<DownloadClientException>(() => XmlRpcSerializer.ParseResponse(xml));

		exception.Message.ShouldContain("bad credentials");
	}

	[Fact]
	public void ParseResponse_ShouldReturnNull_WhenNoParams()
	{
		const string xml = """
			<?xml version="1.0"?><methodResponse><params></params></methodResponse>
			""";

		XmlRpcSerializer.ParseResponse(xml).ShouldBeNull();
	}

	[Fact]
	public void ParseResponse_ShouldParseTopLevelArray()
	{
		const string xml = """
			<?xml version="1.0"?>
			<methodResponse><params><param><value><array><data>
				<value><array><data><value><string>a</string></value></data></array></value>
				<value><array><data><value><string>b</string></value></data></array></value>
			</data></array></value></param></params></methodResponse>
			""";

		var result = XmlRpcSerializer.ParseResponse(xml).ShouldBeOfType<List<object?>>();
		result.Count.ShouldBe(2);
		((List<object?>)result[0]!)[0].ShouldBe("a");
		((List<object?>)result[1]!)[0].ShouldBe("b");
	}
}
