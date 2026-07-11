using System;

namespace Submarine.Core.Release.Exceptions;

/// <summary>
///     Exception thrown when a release is not parsable
/// </summary>
public class NotParsableReleaseException : Exception
{
	/// <summary>
	///     Creates a new instance of <see cref="NotParsableReleaseException" />
	/// </summary>
	/// <param name="reason">The reason why this Release is not parsable</param>
	public NotParsableReleaseException(string reason) : base(reason)
	{
	}
}
