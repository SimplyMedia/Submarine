namespace Submarine.Api.Models.Request;

public record UpdateIndexerConfigRequest
{
	public int RssSyncIntervalMinutes { get; set; }

	public int MinimumAgeMinutes { get; set; }

	public int RetentionDays { get; set; }

	public int MaximumSizeMb { get; set; }
}
