namespace Submarine.Core.Entities;

/// <summary>Stable local identity and selected media version exposed by a compatibility facade.</summary>
public sealed class CompatLibraryBinding : Entity
{
	/// <summary>Facade owning this identity: sonarr or radarr.</summary>
	public string Facade { get; set; } = string.Empty;

	/// <summary>Bound series, when this is a Sonarr identity.</summary>
	public int? SeriesId { get; set; }

	/// <summary>Bound movie, when this is a Radarr identity.</summary>
	public int? MovieId { get; set; }

	/// <summary>Selected version, null only while a binding is excluded or inconsistent.</summary>
	public int? MediaVersionId { get; set; }

	/// <summary>Whether the facade has explicitly excluded this native title.</summary>
	public bool Excluded { get; set; }

	/// <summary>Bound series.</summary>
	public Series? Series { get; set; }

	/// <summary>Bound movie.</summary>
	public Movie? Movie { get; set; }

	/// <summary>Selected media version.</summary>
	public MediaVersion? MediaVersion { get; set; }
}
