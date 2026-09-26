using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A configured import list syncing external watchlists.
/// </summary>
public sealed class ImportList : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>List implementation.</summary>
	public ImportListType Type { get; set; }

	/// <summary>Whether the list is enabled.</summary>
	public bool Enable { get; set; } = true;

	/// <summary>Automatically add items found on the list.</summary>
	public bool EnableAutomaticAdd { get; set; } = true;

	/// <summary>Search on add.</summary>
	public bool SearchOnAdd { get; set; }

	/// <summary>Implementation specific settings as JSON.</summary>
	public string SettingsJson { get; set; } = "{}";

	/// <summary>Whether the list syncs series or movies.</summary>
	public MediaKind MediaKind { get; set; }

	/// <summary>Quality profile for added items.</summary>
	public int? QualityProfileId { get; set; }

	/// <summary>Language profile for added items.</summary>
	public int? LanguageProfileId { get; set; }

	/// <summary>Root folder for added items.</summary>
	public int? RootFolderId { get; set; }

	/// <summary>How new seasons of added series are monitored.</summary>
	public MonitorNewItems Monitor { get; set; } = MonitorNewItems.ALL;

	/// <summary>Minimum availability for added movies.</summary>
	public MinimumAvailability? MinimumAvailability { get; set; }

	/// <summary>Series type for added series.</summary>
	public SeriesType? SeriesType { get; set; }

	/// <summary>Whether added series use season folders.</summary>
	public bool SeasonFolder { get; set; } = true;

	/// <summary>Tags applied to added items.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
