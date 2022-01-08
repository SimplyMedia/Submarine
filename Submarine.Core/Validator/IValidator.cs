namespace Submarine.Core.Validator;

public interface IValidator<in T>
{
	void Validate(T input);
}
