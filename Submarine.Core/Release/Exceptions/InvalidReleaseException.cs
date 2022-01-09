using System;

namespace Submarine.Core.Release.Exceptions;

public class InvalidReleaseException : Exception
{
	public InvalidReleaseException(string reason) : base(reason)
	{
	}
}
