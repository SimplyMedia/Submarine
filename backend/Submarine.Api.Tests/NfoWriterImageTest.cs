using System.Net;
using System.Text;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class NfoWriterImageTest : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	public NfoWriterImageTest()
		=> Directory.CreateDirectory(_root);

	public void Dispose()
	{
		NfoWriterService.HttpClientFactory = null;
		NfoWriterService.Logger = null;

		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	[Fact]
	public void WriteTvShowNfo_ShouldDownloadPosterAndFanart_WhenUrlsSetAndFilesAbsent()
	{
		var handler = new StubHandler(HttpStatusCode.OK, "image-bytes");
		NfoWriterService.HttpClientFactory = new StubHttpClientFactory(handler);

		var series = new Series
		{
			Title = "Show", PosterUrl = "https://images/poster.jpg", BackdropUrl = "https://images/backdrop.jpg"
		};

		NfoWriterService.WriteTvShowNfo(_root, series);

		Assert.True(File.Exists(Path.Combine(_root, "poster.jpg")));
		Assert.True(File.Exists(Path.Combine(_root, "fanart.jpg")));
		Assert.Equal(2, handler.RequestCount);
	}

	[Fact]
	public void WriteMovieNfo_ShouldSkipDownload_WhenFilesAlreadyExist()
	{
		File.WriteAllText(Path.Combine(_root, "poster.jpg"), "existing");
		File.WriteAllText(Path.Combine(_root, "fanart.jpg"), "existing");

		var handler = new StubHandler(HttpStatusCode.OK, "image-bytes");
		NfoWriterService.HttpClientFactory = new StubHttpClientFactory(handler);

		var movie = new Movie
		{
			Title = "Movie", PosterUrl = "https://images/poster.jpg", BackdropUrl = "https://images/backdrop.jpg"
		};

		NfoWriterService.WriteMovieNfo(Path.Combine(_root, "Movie.mkv"), movie);

		Assert.Equal(0, handler.RequestCount);
		Assert.Equal("existing", File.ReadAllText(Path.Combine(_root, "poster.jpg")));
	}

	[Fact]
	public void WriteTvShowNfo_ShouldNotThrow_WhenDownloadReturnsNotFound()
	{
		var handler = new StubHandler(HttpStatusCode.NotFound, "");
		NfoWriterService.HttpClientFactory = new StubHttpClientFactory(handler);

		var series = new Series
		{
			Title = "Show", PosterUrl = "https://images/poster.jpg", BackdropUrl = "https://images/backdrop.jpg"
		};

		NfoWriterService.WriteTvShowNfo(_root, series);

		Assert.False(File.Exists(Path.Combine(_root, "poster.jpg")));
		Assert.False(File.Exists(Path.Combine(_root, "fanart.jpg")));
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly HttpStatusCode _status;
		private readonly string _body;

		public StubHandler(HttpStatusCode status, string body)
		{
			_status = status;
			_body = body;
		}

		public int RequestCount { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			RequestCount++;

			return Task.FromResult(new HttpResponseMessage(_status)
			{
				Content = new ByteArrayContent(Encoding.UTF8.GetBytes(_body))
			});
		}
	}

	private sealed class StubHttpClientFactory : IHttpClientFactory
	{
		private readonly HttpMessageHandler _handler;

		public StubHttpClientFactory(HttpMessageHandler handler)
			=> _handler = handler;

		public HttpClient CreateClient(string name)
			=> new(_handler, disposeHandler: false);
	}
}
