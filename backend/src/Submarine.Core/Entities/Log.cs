namespace Submarine.Core.Entities;

/// <summary>
///     One application log row. Stored in the separate logs database.
/// </summary>
public sealed class Log
{
	/// <summary>Row identity.</summary>
	public int Id { get; set; }

	/// <summary>UTC timestamp of the event.</summary>
	public DateTime Time { get; set; }

	/// <summary>Serilog level name.</summary>
	public string Level { get; set; } = string.Empty;

	/// <summary>Logger name.</summary>
	public string Logger { get; set; } = string.Empty;

	/// <summary>Rendered message.</summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>Exception text, when present.</summary>
	public string? Exception { get; set; }
}
