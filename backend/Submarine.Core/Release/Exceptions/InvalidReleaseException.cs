using System;

namespace Submarine.Core.Release.Exceptions;

/// <summary>
///     Exception thrown when a release is invalid
/// </summary>
public class InvalidReleaseException : Exception
{
	/// <summary>
	///     Creates a new instance of <see cref="InvalidReleaseException" />
	/// </summary>
	/// <param name="reason">The reason why the Release is invalid</param>
	public InvalidReleaseException(string reason) : base(reason)
	{
	}
}
