namespace Submarine.Core.Parser;

/// <summary>
///     A Parser which can parse a string into a specific type
/// </summary>
/// <typeparam name="T">The type to parse to</typeparam>
public interface IParser<out T>
{
	/// <summary>
	///     Parses the input into the specified type
	/// </summary>
	/// <param name="input">The input string</param>
	/// <returns>The generic interface type</returns>
	T Parse(string input);
}
