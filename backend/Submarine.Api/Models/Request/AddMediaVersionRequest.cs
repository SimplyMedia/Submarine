namespace Submarine.Api.Models.Request;

/// <summary>
///     A version to create for a Series or Movie, stored in its own library folder
/// </summary>
public record AddMediaVersionRequest
{
	public string Name { get; set; } = null!;

	public int QualityProfileId { get; set; }

	public int LanguageProfileId { get; set; }

	public int? RootFolderId { get; set; }

	public string? Path { get; set; }

	public bool Monitored { get; set; } = true;
}
