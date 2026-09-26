using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.Entities;

/// <summary>
///     An imported episode video file.
/// </summary>
public sealed class EpisodeFile : Entity
{
	/// <summary>Id of the owning series.</summary>
	public int SeriesId { get; set; }

	/// <summary>Id of the version this file belongs to.</summary>
	public int MediaVersionId { get; set; }

	/// <summary>Path relative to the version folder.</summary>
	public string RelativePath { get; set; } = string.Empty;

	/// <summary>File size in bytes.</summary>
	public long Size { get; set; }

	/// <summary>UTC timestamp of import.</summary>
	public DateTime DateAdded { get; set; }

	/// <summary>Quality of the file.</summary>
	public QualityModel Quality { get; set; } = new(new QualityResolutionModel(), new Revision());

	/// <summary>Languages of the file.</summary>
	public List<Language> Languages { get; set; } = [];

	/// <summary>Release group.</summary>
	public string? ReleaseGroup { get; set; }

	/// <summary>Original scene name, if known.</summary>
	public string? SceneName { get; set; }

	/// <summary>Edition of the file.</summary>
	public string? Edition { get; set; }

	/// <summary>Technical media info.</summary>
	public MediaInfoModel? MediaInfo { get; set; }

	/// <summary>Whether the file name came from the placeholder before renaming.</summary>
	public bool NamedFromPlaceholder { get; set; }

	/// <summary>Owning series.</summary>
	public Series Series { get; set; } = null!;

	/// <summary>Owning version.</summary>
	public MediaVersion MediaVersion { get; set; } = null!;

	/// <summary>Episodes contained in this file.</summary>
	public ICollection<Episode> Episodes { get; set; } = [];
}
