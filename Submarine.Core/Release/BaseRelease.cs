using System;
using System.Collections.Generic;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;

namespace Submarine.Core.Release;

public record BaseRelease
{
	public string FullTitle { get; init; }

	public string Title { get; init; }

	public IReadOnlyList<string> Aliases { get; init; }

	public IReadOnlyList<Language> Languages { get; init; }

	public StreamingProvider? StreamingProvider { get; init; }

	public ReleaseType Type { get; init; }

	public SeriesReleaseData? SeriesReleaseData { get; init; }

	public MovieReleaseData? MovieReleaseData { get; init; }

	public QualityModel Quality { get; init; }

	public Protocols Protocol { get; init; }

	public string? ReleaseGroup { get; init; }

	public DateTimeOffset? CreatedAt { get; init; }

	public BaseRelease()
	{
	}

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
		CreatedAt = source.CreatedAt;
	}

	public TorrentRelease ToTorrent()
		=> new(this);

	public UsenetRelease ToUsenet()
		=> new(this);
}
