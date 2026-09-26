using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A configured notification connection.
/// </summary>
public sealed class Notification : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Notification implementation.</summary>
	public NotificationType Type { get; set; }

	/// <summary>Whether the notification is enabled.</summary>
	public bool Enable { get; set; } = true;

	/// <summary>Implementation specific settings as JSON.</summary>
	public string SettingsJson { get; set; } = "{}";

	/// <summary>Notify on grab.</summary>
	public bool OnGrab { get; set; }

	/// <summary>Notify on import.</summary>
	public bool OnImport { get; set; } = true;

	/// <summary>Notify on upgrade.</summary>
	public bool OnUpgrade { get; set; } = true;

	/// <summary>Notify on rename.</summary>
	public bool OnRename { get; set; }

	/// <summary>Notify on delete.</summary>
	public bool OnDelete { get; set; }

	/// <summary>Notify on health issues.</summary>
	public bool OnHealthIssue { get; set; }

	/// <summary>Notify on health restored.</summary>
	public bool OnHealthRestored { get; set; }

	/// <summary>Notify on application update.</summary>
	public bool OnApplicationUpdate { get; set; }

	/// <summary>Notify when manual interaction is required.</summary>
	public bool OnManualInteractionRequired { get; set; }

	/// <summary>Include health warnings, not only errors.</summary>
	public bool IncludeHealthWarnings { get; set; }

	/// <summary>Tags restricting this notification.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
