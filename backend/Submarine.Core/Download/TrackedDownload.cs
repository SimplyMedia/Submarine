using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;

namespace Submarine.Core.Download;

/// <summary>
///     A download grabbed by Submarine and tracked through its download client until imported
/// </summary>
public class TrackedDownload : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the tracked download
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Id of the Download Client Config this download was sent to
	/// </summary>
	public int DownloadClientConfigId { get; set; }

	/// <summary>
	///     Client-side identifier of the download (e.g. torrent hash or nzo id)
	/// </summary>
	public string DownloadId { get; set; }

	/// <summary>
	///     Title of the download as reported by the client
	/// </summary>
	public string Title { get; set; }

	/// <summary>
	///     Protocol this download was grabbed over
	/// </summary>
	public Protocol Protocol { get; set; }

	/// <summary>
	///     Current status of the download within its client
	/// </summary>
	public DownloadItemStatus Status { get; set; }

	/// <summary>
	///     Title of the grabbed release
	/// </summary>
	public string ReleaseTitle { get; set; }

	/// <summary>
	///     Quality of the grabbed release
	/// </summary>
	public QualityModel Quality { get; set; }

	/// <summary>
	///     Languages of the grabbed release
	/// </summary>
	public List<Language> Languages { get; set; } = new();

	/// <summary>
	///     Release group of the grabbed release, if any
	/// </summary>
	public string? ReleaseGroup { get; set; }

	/// <summary>
	///     Name of the indexer the release originates from, if any
	/// </summary>
	public string? Indexer { get; set; }

	/// <summary>
	///     Path the client downloads into, once known
	/// </summary>
	public string? OutputPath { get; set; }

	/// <summary>
	///     Id of the Series this download is for, if any
	/// </summary>
	public int? SeriesId { get; set; }

	/// <summary>
	///     Id of the Movie this download is for, if any
	/// </summary>
	public int? MovieId { get; set; }

	/// <summary>
	///     Ids of the Episodes this download covers, if any
	/// </summary>
	public List<int> EpisodeIds { get; set; } = new();

	/// <summary>
	///     Whether this download has been imported into the library
	/// </summary>
	public bool Imported { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
