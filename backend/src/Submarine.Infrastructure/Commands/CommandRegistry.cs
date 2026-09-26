using System.Reflection;
using Submarine.Core.Commands;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Maps command names to command record types. Names are the type name minus the Command suffix.
/// </summary>
public sealed class CommandRegistry
{
	private const string Suffix = "Command";

	private readonly Dictionary<string, Type> _types;

	/// <summary>
	///     Discover all ICommand implementations in loaded Submarine assemblies.
	/// </summary>
	public CommandRegistry()
	{
		_types = AppDomain.CurrentDomain.GetAssemblies()
			.Where(assembly => assembly.GetName().Name?.StartsWith("Submarine", StringComparison.Ordinal) == true)
			.SelectMany(GetLoadableTypes)
			.Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(ICommand).IsAssignableFrom(type))
			.GroupBy(GetName, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	///     The registry name of a command type.
	/// </summary>
	public static string GetName(Type type)
		=> type.Name.Length > Suffix.Length && type.Name.EndsWith(Suffix, StringComparison.Ordinal)
			? type.Name[..^Suffix.Length]
			: type.Name;

	/// <summary>
	///     All registered names.
	/// </summary>
	public IReadOnlyCollection<string> Names => _types.Keys;

	/// <summary>
	///     Resolve a command type by name.
	/// </summary>
	/// <exception cref="KeyNotFoundException">Unknown command name.</exception>
	public Type Resolve(string name)
	{
		if (_types.TryGetValue(name, out var type))
		{
			return type;
		}

		throw new KeyNotFoundException($"Unknown command '{name}'");
	}

	/// <summary>
	///     Try to resolve a command type by name.
	/// </summary>
	public bool TryResolve(string name, out Type type)
	{
		if (_types.TryGetValue(name, out var resolved))
		{
			type = resolved;
			return true;
		}

		type = null!;
		return false;
	}

	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException exception)
		{
			return exception.Types.Where(type => type is not null).Select(type => type!);
		}
	}
}
