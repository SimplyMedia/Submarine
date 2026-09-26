using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Submarine.Core.Tests;

[ExcludeFromCodeCoverage]
public class XunitLogger<T> : ILogger<T>, IDisposable
{
	private readonly ITestOutputHelper _output;

	public XunitLogger(ITestOutputHelper output)
		=> _output = output;

	public void Dispose()
	{
		// Can be ignored
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
		Func<TState, Exception, string> formatter)
		=> _output.WriteLine(state?.ToString() ?? string.Empty);

	public bool IsEnabled(LogLevel logLevel)
		=> true;

	public IDisposable BeginScope<TState>(TState state) where TState : notnull
		=> this;
}
