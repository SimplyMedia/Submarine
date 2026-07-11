using System.Collections.Generic;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;

namespace Submarine.Core.Release;

/// <summary>
///     The base class for all releases, contains all common properties
/// </summary>
public record BaseRelease
{
	/// <summary>
	///     The Full Title of the Release
	/// </summary>
	public string FullTitle { get; init; }

	/// <summary>
	///     The parsed Title of the Release
	/// </summary>
	public string Title { get; init; }

	/// <summary>
	///     The Year included in the Release Title, if any
	/// </summary>
	public int? Year { get; init; }

	/// <summary>
	///     Aliases for the Release, if any
	/// </summary>
	public IReadOnlyList<string> Aliases { get; init; }

	/// <summary>
	///     Languages included in the Release Title, if any
	/// </summary>
	public IReadOnlyList<Language> Languages { get; init; }

	/// <summary>
	///     Streaming Provider of this Release, if any
	/// </summary>
	public StreamingProvider? StreamingProvider { get; init; }

	/// <summary>
	///     The Type of Release
	/// </summary>
	public ReleaseType Type { get; init; }

	/// <summary>
	///     Release data for Series Releases, if any
	/// </summary>
	public SeriesReleaseData? SeriesReleaseData { get; init; }

	/// <summary>
	///     Release data for Movie Releases, if any
	/// </summary>
	public MovieReleaseData? MovieReleaseData { get; init; }

	/// <summary>
	///     The Quality of the Release
	/// </summary>
	public QualityModel Quality { get; init; }

	/// <summary>
	///     The Protocol of the Release
	/// </summary>
	public Protocol Protocol { get; init; }

	/// <summary>
	///     The Release Group of the Release, if any
	/// </summary>
	public string? ReleaseGroup { get; init; }

	/// <summary>
	///     The Hash of the Release, if any
	/// </summary>
	public string? ReleaseHash { get; init; }

	/// <summary>
	///     Creates a new instance of <see cref="BaseRelease" />
	/// </summary>
	public BaseRelease()
	{
	}

	/// <summary>
	///     Creates a new instance of <see cref="BaseRelease" />, used in Inheritance
	/// </summary>
	/// <param name="source">The source base release</param>
	public BaseRelease(BaseRelease source)
	{
		FullTitle = source.FullTitle;
		Title = source.Title;
		Aliases = source.Aliases;
		Languages = source.Languages;
		StreamingProvider = source.StreamingProvider;
		Type = source.Type;
		SeriesReleaseData = source.SeriesReleaseData;
		MovieReleaseData = source.MovieReleaseData;
		Quality = source.Quality;
		Protocol = source.Protocol;
		ReleaseGroup = source.ReleaseGroup;
	}

	/// <summary>
	///     Converts this Release to a Torrent Release
	/// </summary>
	/// <returns>This release as a TorrentRelease</returns>
	public TorrentRelease ToTorrent()
		=> new(this);


	/// <summary>
	///     Converts this Release to a Usenet Release
	/// </summary>
	/// <returns>This release as a Usenet Release</returns>
	public UsenetRelease ToUsenet()
		=> new(this);
}
