using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers.Cardigann;

/// <summary>
///     Deserializes Cardigann v11 YAML definitions
/// </summary>
public static class CardigannDefinitionParser
{
	private static readonly IDeserializer Deserializer = new DeserializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.WithTypeConverter(new ScalarToListConverter())
		.Build();

	/// <summary>
	///     Parses a definition from YAML text
	/// </summary>
	/// <param name="yaml">The definition YAML</param>
	/// <param name="fallbackId">The id used when the YAML has none, usually the file name</param>
	/// <returns>The parsed definition</returns>
	/// <exception cref="IndexerException">When the YAML cannot be parsed</exception>
	public static CardigannDefinition Parse(string yaml, string? fallbackId = null)
	{
		CardigannDefinition definition;
		try
		{
			definition = Deserializer.Deserialize<CardigannDefinition>(yaml);
		}
		catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or ArgumentException)
		{
			throw new IndexerException($"Definition could not be parsed: {exception.Message}", exception);
		}

		if (string.IsNullOrWhiteSpace(definition.Id) && fallbackId is { })
			definition = definition with { Id = fallbackId };

		if (string.IsNullOrWhiteSpace(definition.Id))
			throw new IndexerException("Definition has no id");

		if (string.IsNullOrWhiteSpace(definition.Name))
			definition = definition with { Name = definition.Id };

		return definition;
	}
}

/// <summary>
///     Cardigann filters accept a single scalar where a list of arguments is expected
/// </summary>
internal sealed class ScalarToListConverter : YamlDotNet.Serialization.IYamlTypeConverter
{
	public bool Accepts(Type type)
		=> type == typeof(List<string>);

	public object? ReadYaml(YamlDotNet.Core.IParser parser, Type type, YamlDotNet.Serialization.ObjectDeserializer rootDeserializer)
	{
		if (parser.Current is YamlDotNet.Core.Events.Scalar scalar)
		{
			parser.MoveNext();
			return scalar.Value is null || (scalar.Value.Length == 0 && scalar.Style == YamlDotNet.Core.ScalarStyle.Plain)
				? new List<string>()
				: [scalar.Value];
		}

		if (parser.Current is YamlDotNet.Core.Events.SequenceStart)
		{
			// delegate per item to avoid re-entering this converter for the list type itself
			parser.MoveNext();
			var list = new List<string>();
			while (parser.Current is not YamlDotNet.Core.Events.SequenceEnd)
			{
				var item = rootDeserializer(typeof(string));
				list.Add(item as string ?? string.Empty);
			}

			parser.MoveNext();
			return list;
		}

		while (parser.Current is not (YamlDotNet.Core.Events.SequenceEnd or null))
			parser.MoveNext();
		if (parser.Current is not null)
			parser.MoveNext();
		return new List<string>();
	}

	public void WriteYaml(YamlDotNet.Core.IEmitter emitter, object? value, Type type, YamlDotNet.Serialization.ObjectSerializer rootSerializer)
		=> throw new NotSupportedException("Definitions are only deserialized");
}
