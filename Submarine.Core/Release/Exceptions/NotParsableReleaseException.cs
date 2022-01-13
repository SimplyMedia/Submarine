using System;

namespace Submarine.Core.Release.Exceptions;

public class NotParsableReleaseException : Exception
{
	public NotParsableReleaseException(string reason) : base(reason)
	{
	}
}
