namespace Submarine.Api.Models.Request;

public record UpdateMediaManagementConfigRequest
{
	public bool UseHardlinks { get; set; }

	public bool ImportExtraFiles { get; set; }

	public int MinimumFreeSpaceMb { get; set; }

	public bool WriteNfo { get; set; }
}
