using AspNetCore.ExceptionHandler.Attributes;

namespace Submarine.Api.Exceptions;

[StatusCode(404)]
public class NotFoundException : Exception
{
}
