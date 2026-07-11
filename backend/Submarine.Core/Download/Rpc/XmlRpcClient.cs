using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Xml.Linq;

namespace Submarine.Core.Download.Rpc;

/// <summary>
///     Minimal XML-RPC client over <see cref="HttpClient" />, covering the value types rTorrent uses
/// </summary>
public class XmlRpcClient
{
	private readonly HttpClient _httpClient;

	/// <summary>
	///     Creates a new instance of <see cref="XmlRpcClient" />
	/// </summary>
	/// <param name="httpClient">http client used to post the requests</param>
	public XmlRpcClient(HttpClient httpClient)
		=> _httpClient = httpClient;

	/// <summary>
	///     Calls an XML-RPC method and returns its parsed response value
	/// </summary>
	/// <param name="endpoint">absolute url of the XML-RPC endpoint</param>
	/// <param name="methodName">name of the method to call</param>
	/// <param name="parameters">method parameters (string, int, long, bool, byte[] or arrays of those)</param>
	/// <param name="cancellationToken">token to cancel the operation</param>
	/// <returns>the parsed method response value</returns>
	/// <exception cref="DownloadClientException">Thrown when the server responds with a fault</exception>
	public async Task<object?> CallAsync(string endpoint, string methodName, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken = default)
	{
		var content = new StringContent(BuildRequest(methodName, parameters), Encoding.UTF8, "text/xml");
		var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
		response.EnsureSuccessStatusCode();

		return ParseResponse(await response.Content.ReadAsStringAsync(cancellationToken));
	}

	/// <summary>
	///     Serializes a <c>methodCall</c> document for the given method and parameters
	/// </summary>
	public static string BuildRequest(string methodName, IReadOnlyList<object?> parameters)
	{
		var call = new XElement("methodCall",
			new XElement("methodName", methodName),
			new XElement("params", parameters.Select(p => new XElement("param", SerializeValue(p)))));

		return "<?xml version=\"1.0\"?>" + call.ToString(SaveOptions.DisableFormatting);
	}

	/// <summary>
	///     Parses a <c>methodResponse</c> document into a value, throwing on faults
	/// </summary>
	/// <exception cref="DownloadClientException">Thrown when the document is a fault</exception>
	public static object? ParseResponse(string xml)
	{
		var root = XDocument.Parse(xml).Root
			?? throw new DownloadClientException("XML-RPC response was empty");

		var fault = root.Element("fault");
		if (fault is not null)
		{
			var members = (Dictionary<string, object?>)ParseValue(fault.Element("value")!)!;
			throw new DownloadClientException(
				$"XML-RPC fault: {members.GetValueOrDefault("faultString") ?? "unknown"}");
		}

		var value = root.Element("params")?.Element("param")?.Element("value");

		return value is null ? null : ParseValue(value);
	}

	private static XElement SerializeValue(object? value)
	{
		XElement inner = value switch
		{
			null => new XElement("string", string.Empty),
			string s => new XElement("string", s),
			bool b => new XElement("boolean", b ? "1" : "0"),
			int i => new XElement("i4", i.ToString(CultureInfo.InvariantCulture)),
			long l => new XElement("i8", l.ToString(CultureInfo.InvariantCulture)),
			byte[] bytes => new XElement("base64", Convert.ToBase64String(bytes)),
			IEnumerable<object?> array => new XElement("array",
				new XElement("data", array.Select(SerializeValue))),
			_ => throw new ArgumentException($"Unsupported XML-RPC value type: {value.GetType()}")
		};

		return new XElement("value", inner);
	}

	private static object? ParseValue(XElement value)
	{
		var typed = value.Elements().FirstOrDefault();
		if (typed is null)
			return value.Value;

		return typed.Name.LocalName switch
		{
			"string" => typed.Value,
			"i4" or "int" => int.Parse(typed.Value, CultureInfo.InvariantCulture),
			"i8" => long.Parse(typed.Value, CultureInfo.InvariantCulture),
			"boolean" => typed.Value.Trim() == "1",
			"base64" => Convert.FromBase64String(typed.Value),
			"array" => typed.Element("data")?.Elements("value").Select(ParseValue).ToList() ?? [],
			"struct" => typed.Elements("member")
				.ToDictionary(m => m.Element("name")!.Value, m => ParseValue(m.Element("value")!)),
			_ => typed.Value
		};
	}
}
