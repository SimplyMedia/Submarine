namespace Submarine.Api.Models.Request;

public record UpdateMediaManagementConfigRequest
{
	public bool UseHardlinks { get; set; }

	public bool ImportExtraFiles { get; set; }

	public int MinimumFreeSpaceMb { get; set; }

	public bool WriteNfo { get; set; }

	public string? ChmodFolder { get; set; }

	public string? ChmodFile { get; set; }

	public string? ChownUser { get; set; }

	public string? ChownGroup { get; set; }
}
