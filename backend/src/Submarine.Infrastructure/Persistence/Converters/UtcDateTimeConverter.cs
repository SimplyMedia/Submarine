using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Submarine.Infrastructure.Persistence.Converters;

/// <summary>
///     Stores and reads every DateTime as UTC. Values are never shifted across time zones,
///     the Kind is normalized to Utc in both directions.
/// </summary>
public sealed class UtcDateTimeConverter()
	: ValueConverter<DateTime, DateTime>(
		value => DateTime.SpecifyKind(value, DateTimeKind.Utc),
		value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
