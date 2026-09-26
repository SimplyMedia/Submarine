using System.Net;
using Xunit;
using System.Text;
using Shouldly;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Mappings;

namespace Submarine.Infrastructure.Tests.Metadata;

/// <summary>
///     HTTP behaviour of the Metadata and Mappings clients with stub responses.
/// </summary>
public sealed class SiblingClientTests
{
	[Fact]
	public async Task MetadataClient_ShouldReturnNull_WhenDetailIs404()
	{
		var client = Client(HttpStatusCode.NotFound, "{}");
		var series = await new MetadataClient(client).GetSeriesByTvdbAsync(123);
		var movie = await new MetadataClient(client).GetMovieAsync(456);
		var collection = await new MetadataClient(client).GetCollectionAsync(789);
		series.ShouldBeNull();
		movie.ShouldBeNull();
		collection.ShouldBeNull();
	}

	[Fact]
	public async Task MetadataClient_ShouldDeserializeEnumsAsStrings()
	{
		var json = """
			{
				"tvdbId": 1, "tmdbId": 2, "imdbId": "tt1", "title": "Test", "sortTitle": null,
				"overview": null, "firstAired": "2024-01-01", "status": "CONTINUING", "runtime": 30,
				"network": "Net", "genres": [], "certification": null,
				"seasons": [{"seasonNumber": 1, "name": "One", "episodeCount": 2}],
				"episodes": [{
					"tvdbId": 10, "tmdbId": null, "title": "Pilot", "overview": null,
					"airDate": "2024-01-01", "airDateUtc": "2024-01-01", "runtime": 30,
					"numbers": [{"ordering": "AIRED", "seasonNumber": 1, "number": 1, "absoluteNumber": 1}],
					"imageUrl": null
				}],
				"posterUrl": null, "backdropUrl": null, "year": 2024, "alternateTitles": []
			}
			""";
		var series = await new MetadataClient(Client(HttpStatusCode.OK, json)).GetSeriesByTvdbAsync(1);

		series.ShouldNotBeNull();
		series!.Status.ShouldBe(Contracts.Metadata.SeriesStatus.CONTINUING);
		series.Episodes.Single().Numbers.Single().Ordering.ShouldBe(Contracts.Metadata.EpisodeOrdering.AIRED);
		series.Episodes.Single().Numbers.Single().Number.ShouldBe(1);
	}

	[Fact]
	public async Task MetadataClient_ShouldThrow_WhenSearchFails()
	{
		var client = Client(HttpStatusCode.InternalServerError, "boom");
		await Should.ThrowAsync<HttpRequestException>(
			async () => await new MetadataClient(client).SearchSeriesAsync("test", Core.Enums.MetadataProvider.TVDB));
	}

	[Fact]
	public async Task MappingsClient_ShouldResolveSceneAndReturnEmptySetsForUnknown()
	{
		var json = """
			{
				"tvdbId": 5,
				"mappings": [{"id": 1, "tvdbId": 5, "title": "t", "seasonNumber": null, "sceneSeasonNumber": 1, "episodeOffset": 0, "searchTitle": null, "comment": null}],
				"episodeMappings": []
			}
			""";
		var mappings = await new MappingsClient(Client(HttpStatusCode.OK, json)).GetSceneMappingsAsync(5);

		mappings.TvdbId.ShouldBe(5);
		mappings.Mappings.ShouldHaveSingleItem();
		mappings.EpisodeMappings.ShouldBeEmpty();
	}

	[Fact]
	public async Task ApiKeyHandler_ShouldAddHeader_WhenApiKeyConfigured()
	{
		HttpRequestMessage? captured = null;
		var handler = new ApiKeyHandler("secret-key") { InnerHandler = new CapturingHandler(request => captured = request) };
		using var invoker = new HttpMessageInvoker(handler);

		await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/x"), CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.Headers.GetValues("X-Api-Key").ShouldBe(["secret-key"]);
	}

	[Fact]
	public async Task ApiKeyHandler_ShouldNotAddHeader_WhenNoApiKeyConfigured()
	{
		HttpRequestMessage? captured = null;
		var handler = new ApiKeyHandler(null) { InnerHandler = new CapturingHandler(request => captured = request) };
		using var invoker = new HttpMessageInvoker(handler);

		await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/x"), CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.Headers.Contains("X-Api-Key").ShouldBeFalse();
	}

	[Fact]
	public async Task SiblingResponseHandler_ShouldThrowNamingTheService_WhenResponseIsServerError()
	{
		var handler = new SiblingResponseHandler("Metadata service")
		{
			InnerHandler = new RespondingHandler(HttpStatusCode.BadGateway, "boom")
		};
		using var invoker = new HttpMessageInvoker(handler);

		var exception = await Should.ThrowAsync<SiblingServiceException>(
			() => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost/x"), CancellationToken.None));

		exception.ServiceName.ShouldBe("Metadata service");
		exception.Message.ShouldContain("Metadata service");
	}

	[Fact]
	public async Task SiblingResponseHandler_ShouldThrowNamingTheService_WhenTransportFails()
	{
		var handler = new SiblingResponseHandler("Mappings service")
		{
			InnerHandler = new ThrowingHandler()
		};
		using var invoker = new HttpMessageInvoker(handler);

		var exception = await Should.ThrowAsync<SiblingServiceException>(
			() => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost/x"), CancellationToken.None));

		exception.ServiceName.ShouldBe("Mappings service");
	}

	[Fact]
	public async Task SiblingResponseHandler_ShouldPassThrough_NotFoundAndSuccess()
	{
		var notFoundHandler = new SiblingResponseHandler("Metadata service") { InnerHandler = new RespondingHandler(HttpStatusCode.NotFound, "") };
		using var notFoundInvoker = new HttpMessageInvoker(notFoundHandler);
		var notFoundResponse = await notFoundInvoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost/x"), CancellationToken.None);
		notFoundResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		var okHandler = new SiblingResponseHandler("Metadata service") { InnerHandler = new RespondingHandler(HttpStatusCode.OK, "{}") };
		using var okInvoker = new HttpMessageInvoker(okHandler);
		var okResponse = await okInvoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost/x"), CancellationToken.None);
		okResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private sealed class RespondingHandler(HttpStatusCode status, string body) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
	}

	private sealed class ThrowingHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> throw new HttpRequestException("connection refused");
	}

	private sealed class CapturingHandler(Action<HttpRequestMessage> capture) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			capture(request);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		}
	}

	private static HttpClient Client(HttpStatusCode status, string json)
		=> new(new StubHandler(status, json))
		{
			BaseAddress = new Uri("http://localhost:1")
		};

	private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var requested = request.RequestUri!.PathAndQuery;
			if (requested.StartsWith("/api/v1/scene/5/resolve", StringComparison.Ordinal))
			{
				return Respond(HttpStatusCode.OK, """{"sceneSeason": 1, "sceneEpisode": 2}""");
			}

			return Respond(status, json);
		}

		private static Task<HttpResponseMessage> Respond(HttpStatusCode code, string payload)
			=> Task.FromResult(new HttpResponseMessage(code)
			{
				Content = new StringContent(payload, Encoding.UTF8, "application/json")
			});
	}
}
