using System.Net;

namespace Submarine.Metadata.Tests.Support;

/// <summary>
///     Recorded upstream payload fixtures shaped after the documented
///     TMDB v3 and TVDB v4 responses.
/// </summary>
internal static class Fixtures
{
	public const string TvdbLoginA = """{"status":"success","data":{"token":"token-a"}}""";

	public const string TvdbLoginB = """{"status":"success","data":{"token":"token-b"}}""";

	public const string TvdbSeriesExtended =
		"""
		{"status":"success","data":{"id":75760,"name":"Wallander","overview":"Kurt Wallander solves crimes in Ystad.","firstAired":"2005-09-18","status":{"id":2,"name":"Ended"},"averageRuntime":90,"poster":"https://artworks.thetvdb.com/banners/posters/75760-1.jpg","backdrops":["https://artworks.thetvdb.com/banners/fanart/original/75760-1.jpg"],"genres":[{"id":3,"name":"Crime"},{"id":18,"name":"Drama"}],"aliases":[{"language":"swe","alias":"Kommissar Wallander"}],"companies":[{"id":10,"name":"TV4","type":{"id":4,"name":"Network"}}],"remoteIds":[{"id":48891,"type":{"id":2,"name":"TMDB"}},{"id":"tt0836958","type":{"id":13,"name":"IMDB"}}],"seasons":[{"id":100,"seasonNumber":1,"type":{"id":1,"name":"default"},"episodeCount":2},{"id":101,"seasonNumber":2,"type":{"id":1,"name":"default"},"episodeCount":1},{"id":200,"seasonNumber":1,"type":{"id":3,"name":"dvd"},"episodeCount":3}]}}
		""";

	public const string TvdbEpisodesDefault =
		"""
		{"status":"success","data":{"episodes":[{"id":1001,"seasonNumber":1,"number":1,"absoluteNumber":1,"name":"Sidetracked","overview":"A bank robbery turns into a siege.","aired":"2005-09-18","runtime":90,"image":"https://artworks.thetvdb.com/episodes/1001.jpg"},{"id":1002,"seasonNumber":1,"number":2,"absoluteNumber":2,"name":"The Village Idiot","overview":"Wallander investigates a bird flu outbreak.","aired":"2005-09-25","runtime":90,"image":null},{"id":1003,"seasonNumber":2,"number":1,"absoluteNumber":3,"name":"The African","overview":"A refugee is found dead.","aired":"2006-01-01","runtime":90,"image":null}],"links":{"next":null}}}
		""";

	public const string TvdbEpisodesDvd =
		"""
		{"status":"success","data":{"episodes":[{"id":1001,"seasonNumber":1,"number":1,"name":"Sidetracked","aired":"2005-09-18","runtime":90},{"id":1002,"seasonNumber":1,"number":2,"name":"The Village Idiot","aired":"2005-09-25","runtime":90},{"id":1003,"seasonNumber":2,"number":1,"name":"The African","aired":"2006-01-01","runtime":90}],"links":{"next":null}}}
		""";

	public const string TvdbEpisodesDvdPartial =
		"""
		{"status":"success","data":{"episodes":[{"id":1001,"seasonNumber":1,"number":1,"name":"Sidetracked","aired":"2005-09-18","runtime":90},{"id":1002,"seasonNumber":1,"number":2,"name":"The Village Idiot","aired":"2005-09-25","runtime":90}],"links":{"next":null}}}
		""";

	public const string TvdbEpisodesAbsolute =
		"""
		{"status":"success","data":{"episodes":[{"id":1001,"seasonNumber":1,"number":1,"absoluteNumber":1},{"id":1002,"seasonNumber":1,"number":2,"absoluteNumber":2},{"id":1003,"seasonNumber":2,"number":3,"absoluteNumber":3}],"links":{"next":null}}}
		""";

	public const string TvdbEpisodesAbsoluteWithZero =
		"""
		{"status":"success","data":{"episodes":[{"id":1001,"seasonNumber":1,"number":1,"absoluteNumber":1},{"id":1002,"seasonNumber":1,"number":0,"absoluteNumber":0},{"id":1003,"seasonNumber":2,"number":0,"absoluteNumber":0}],"links":{"next":null}}}
		""";

	public const string TvdbTranslations =
		"""
		{"status":"success","data":{"nameTranslations":[{"language":"deu","name":"Kommissar Wallander im fernen Land"},{"language":"swe","name":"Wallander"}]}}
		""";

	public const string TvdbSearch =
		"""
		{"status":"success","data":[{"tvdbId":75760,"type":"series","name":"Wallander","year":"2005","overview":"Kurt Wallander solves crimes in Ystad.","image_url":"https://artworks.thetvdb.com/banners/posters/75760-1.jpg","status":{"id":2,"name":"Ended"}}]}
		""";

	public const string TmdbSeriesDetail =
		"""
		{"id":1399,"name":"Game of Thrones","original_name":"Game of Thrones","overview":"Seven noble families fight for control of Westeros.","first_air_date":"2011-04-17","status":"Ended","episode_run_time":[52,60],"genres":[{"id":18,"name":"Drama"}],"networks":[{"id":49,"name":"HBO"}],"poster_path":"/1XS1oqL89opfnbLl8WnZY1O1lJx.jpg","backdrop_path":"/suopoADq0k8YZr4dQXcU6pToj6s.jpg","number_of_seasons":2,"seasons":[{"season_number":1,"name":"Season 1","episode_count":2},{"season_number":2,"name":"Season 2","episode_count":1}],"external_ids":{"tvdb_id":121361,"imdb_id":"tt0944947"},"alternative_titles":{"results":[{"title":"Game of Thrones: Das Lied von Eis und Feuer","iso_3166_1":"DE"}]},"content_ratings":{"results":[{"iso_3166_1":"DE","rating":"16"},{"iso_3166_1":"US","rating":"TV-MA"}]},"episode_groups":{"results":[{"id":"group-absolute","name":"Absolute order","type":2},{"id":"group-dvd","name":"DVD order","type":3}]}}
		""";

	public const string TmdbSeriesDetailWithoutGroups =
		"""
		{"id":1399,"name":"Game of Thrones","original_name":"Game of Thrones","overview":"Seven noble families fight for control of Westeros.","first_air_date":"2011-04-17","status":"Returning Series","episode_run_time":[60],"genres":[{"id":18,"name":"Drama"}],"networks":[{"id":49,"name":"HBO"}],"poster_path":"/1XS1oqL89opfnbLl8WnZY1O1lJx.jpg","backdrop_path":"/suopoADq0k8YZr4dQXcU6pToj6s.jpg","seasons":[{"season_number":1,"name":"Season 1","episode_count":2}],"external_ids":{"tvdb_id":121361,"imdb_id":"tt0944947"}}
		""";

	public const string TmdbSeason1 =
		"""
		{"id":3572,"name":"Season 1","season_number":1,"episodes":[{"id":63056,"name":"Winter Is Coming","overview":"Ned Stark is torn between his family and an old friend.","air_date":"2011-04-17","runtime":62,"season_number":1,"episode_number":1,"still_path":"/wGFUewXPeMErCe2fCuw3JI4JsCI.jpg"},{"id":63057,"name":"The Kingsroad","overview":"The Starks travel south.","air_date":"2011-04-24","runtime":56,"season_number":1,"episode_number":2,"still_path":null}]}
		""";

	public const string TmdbSeason2 =
		"""
		{"id":3624,"name":"Season 2","season_number":2,"episodes":[{"id":63058,"name":"The North Remembers","overview":"Robb rides south.","air_date":"2012-04-01","runtime":57,"season_number":2,"episode_number":1,"still_path":null}]}
		""";

	public const string TmdbGroupAbsolute =
		"""
		{"id":"group-absolute","name":"Absolute order","groups":[{"order":0,"episodes":[{"id":"63056","order":0,"season_number":1,"episode_number":1},{"id":"63057","order":1,"season_number":1,"episode_number":2},{"id":"63058","order":2,"season_number":2,"episode_number":1}]}]}
		""";

	public const string TmdbGroupDvd =
		"""
		{"id":"group-dvd","name":"DVD order","groups":[{"order":1,"episodes":[{"id":"63056","order":0},{"id":"63057","order":1}]},{"order":2,"episodes":[{"id":"63058","order":0}]}]}
		""";

	public const string TmdbMovieDetail =
		"""
		{"id":27205,"title":"Inception","original_title":"Inception","overview":"A thief who steals secrets from dreams.","release_date":"2010-07-16","runtime":148,"genres":[{"id":878,"name":"Science Fiction"},{"id":28,"name":"Action"}],"production_companies":[{"id":9996,"name":"Syncopy"},{"id":13769,"name":"Warner Bros. Pictures"}],"status":"Released","poster_path":"/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg","backdrop_path":"/s3TBrRGB1iav7gFOCNx3H31MoES.jpg","belongs_to_collection":{"id":525,"name":"Inception Collection"},"release_dates":{"results":[{"iso_3166_1":"US","release_dates":[{"certification":"PG-13","note":"Premiere","release_date":"2010-07-13T00:00:00Z","type":1},{"certification":"","note":"","release_date":"2010-07-16T00:00:00Z","type":3},{"certification":"PG-13","note":"4K UHD","release_date":"2017-12-19T00:00:00Z","type":4},{"certification":"PG-13","note":"Blu-ray","release_date":"2010-12-07T00:00:00Z","type":5}]},{"iso_3166_1":"DE","release_dates":[{"certification":"12","note":"","release_date":"2010-07-22T00:00:00Z","type":3}]}]},"alternative_titles":{"results":[{"title":"Origen","iso_3166_1":"ES"},{"title":"Inception","iso_3166_1":"US"}]},"videos":{"results":[{"key":"YoHD9XEInc0","site":"YouTube","type":"Trailer","official":true},{"key":"teaserKey","site":"YouTube","type":"Teaser","official":false}]},"external_ids":{"imdb_id":"tt1375666"}}
		""";

	public const string TmdbMovieSearch =
		"""
		{"page":1,"results":[{"id":27205,"title":"Inception","overview":"A thief who steals secrets from dreams.","release_date":"2010-07-16","poster_path":"/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg","backdrop_path":"/s3TBrRGB1iav7gFOCNx3H31MoES.jpg","original_title":"Inception"}],"total_pages":1,"total_results":1}
		""";

	public const string TmdbSeriesSearch =
		"""
		{"page":1,"results":[{"id":1399,"name":"Game of Thrones","overview":"Seven noble families fight for control of Westeros.","first_air_date":"2011-04-17","poster_path":"/1XS1oqL89opfnbLl8WnZY1O1lJx.jpg","backdrop_path":"/suopoADq0k8YZr4dQXcU6pToj6s.jpg"}],"total_pages":1,"total_results":1}
		""";

	public const string TmdbFind =
		"""
		{"movie_results":[{"id":27205,"title":"Inception","overview":"A thief who steals secrets from dreams.","release_date":"2010-07-16","poster_path":"/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg","original_title":"Inception"}],"tv_results":[],"person_results":[]}
		""";

	public const string TmdbCollection =
		"""
		{"id":525,"name":"Inception Collection","overview":"Nolan's dream heist films.","poster_path":"/8xpDnsdXNA5CL8gABeRKWDee3Z4.jpg","parts":[{"id":27205,"title":"Inception","overview":"A thief who steals secrets from dreams.","release_date":"2010-07-16","poster_path":"/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg","original_title":"Inception"}]}
		""";

	/// <summary>Queues a complete successful TVDB series fetch with the given ordering pages.</summary>
	public static StubHttpMessageHandler EnqueueTvdbSeries(
		this StubHttpMessageHandler handler,
		string dvdEpisodes,
		string absoluteEpisodes,
		string login = TvdbLoginA)
		=> handler
			.Respond(HttpStatusCode.OK, login)
			.Respond(HttpStatusCode.OK, TvdbSeriesExtended)
			.Respond(HttpStatusCode.OK, TvdbEpisodesDefault)
			.Respond(HttpStatusCode.OK, dvdEpisodes)
			.Respond(HttpStatusCode.OK, absoluteEpisodes)
			.Respond(HttpStatusCode.OK, TvdbTranslations);

	/// <summary>Queues a complete successful TMDB series fetch without episode groups.</summary>
	public static StubHttpMessageHandler EnqueueTmdbSeriesWithoutGroups(this StubHttpMessageHandler handler)
		=> handler
			.Respond(HttpStatusCode.OK, TmdbSeriesDetailWithoutGroups)
			.Respond(HttpStatusCode.OK, TmdbSeason1);

	/// <summary>Queues a complete successful TMDB series fetch with absolute and DVD episode groups.</summary>
	public static StubHttpMessageHandler EnqueueTmdbSeriesWithGroups(this StubHttpMessageHandler handler)
		=> handler
			.Respond(HttpStatusCode.OK, TmdbSeriesDetail)
			.Respond(HttpStatusCode.OK, TmdbSeason1)
			.Respond(HttpStatusCode.OK, TmdbSeason2)
			.Respond(HttpStatusCode.OK, TmdbGroupAbsolute)
			.Respond(HttpStatusCode.OK, TmdbGroupDvd);
}
