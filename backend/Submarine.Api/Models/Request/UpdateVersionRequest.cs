namespace Submarine.Api.Models.Request;

/// <summary>
///     A partial update to a <see cref="Submarine.Core.Library.MediaVersion" />
/// </summary>
public record UpdateVersionRequest
{
	public string? Name { get; set; }

	public int? QualityProfileId { get; set; }

	public int? LanguageProfileId { get; set; }

	public bool? Monitored { get; set; }

	public string? Path { get; set; }

	public int? RootFolderId { get; set; }
}
