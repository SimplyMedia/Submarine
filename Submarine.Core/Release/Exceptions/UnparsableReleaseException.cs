using System;

namespace Submarine.Core.Release.Exceptions;

public class UnparsableReleaseException : Exception
{
	public UnparsableReleaseException(string reason) : base(reason)
	{
	}
}
