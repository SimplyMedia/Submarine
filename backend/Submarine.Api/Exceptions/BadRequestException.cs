using AspNetCore.ExceptionHandler.Attributes;

namespace Submarine.Api.Exceptions;

[StatusCode(400)]
public class BadRequestException : Exception
{
	public BadRequestException(string message) : base(message)
	{
	}
}
