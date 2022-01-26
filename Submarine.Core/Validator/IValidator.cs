namespace Submarine.Core.Validator;

/// <summary>
///     IValidator validates the given input is correct and valid
/// </summary>
/// <typeparam name="T">Type of input</typeparam>
public interface IValidator<in T>
{
	/// <summary>
	///     Validate that <see cref="input" /> is valid
	/// </summary>
	/// <param name="input">the input to validate</param>
	void Validate(T input);
}
