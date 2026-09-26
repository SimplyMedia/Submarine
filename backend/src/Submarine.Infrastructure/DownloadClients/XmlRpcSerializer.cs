using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Submarine.Core.Download;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     Minimal XML-RPC encoder and decoder, covering the value types rTorrent uses
/// </summary>
internal static class XmlRpcSerializer
{
	/// <summary>
	///     Serializes a methodCall document for the given method and parameters
	/// </summary>
	public static string BuildRequest(string methodName, IReadOnlyList<object?> parameters)
	{
		var call = new XElement("methodCall",
			new XElement("methodName", methodName),
			new XElement("params", parameters.Select(parameter => new XElement("param", SerializeValue(parameter)))));

		return "<?xml version=\"1.0\"?>" + call.ToString(SaveOptions.DisableFormatting);
	}

	/// <summary>
	///     Parses a methodResponse document into a value, throwing on faults
	/// </summary>
	/// <exception cref="DownloadClientException">The document is a fault or empty</exception>
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
			IDictionary<string, object?> structDictionary => new XElement("struct",
				structDictionary.Select(pair => new XElement("member",
					new XElement("name", pair.Key), SerializeValue(pair.Value)))),
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
				.ToDictionary(member => member.Element("name")!.Value, member => ParseValue(member.Element("value")!)),
			_ => typed.Value
		};
	}
}
