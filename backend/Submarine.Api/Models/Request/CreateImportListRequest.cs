using Submarine.Core.ImportList;
using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record CreateImportListRequest
{
	public string Name { get; set; }

	public ImportListType Type { get; set; }

	public bool Enable { get; set; }

	public string SettingsJson { get; set; }

	public MediaKind MediaKind { get; set; }

	public int QualityProfileId { get; set; }

	public int LanguageProfileId { get; set; }

	public int RootFolderId { get; set; }

	public bool Monitored { get; set; } = true;

	public List<string> Tags { get; set; } = new();
}
