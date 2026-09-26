namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Detects build time tooling that loads the app without a real runtime environment.
/// </summary>
public static class SubmarineDesignTime
{
	/// <summary>
	///     True when the process is the build time OpenAPI document generator, which
	///     runs the app in-process as GetDocument.Insider (formerly dotnet-getdocument).
	/// </summary>
	public static bool IsDocumentGeneration()
	{
		var commandLine = Environment.CommandLine;
		return commandLine.Contains("GetDocument.Insider", StringComparison.Ordinal)
			|| commandLine.Contains("dotnet-getdocument", StringComparison.Ordinal);
	}
}
