using AspNetCore.ExceptionHandler.Attributes;

namespace Submarine.Api.Exceptions;

[StatusCode(409)]
public class ConflictException : Exception
{
	public ConflictException(string message) : base(message)
	{
	}
}
