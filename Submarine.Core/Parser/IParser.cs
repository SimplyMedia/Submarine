namespace Submarine.Core.Parser;

public interface IParser<out T>
{
	T Parse(string input);
}
