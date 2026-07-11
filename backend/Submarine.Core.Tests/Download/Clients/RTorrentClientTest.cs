using System.Net;
using System.Net.Http;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class RTorrentClientTest
{
	private const string Magnet =
		"magnet:?xt=urn:btih:abcdef1234567890abcdef1234567890abcdef12&dn=Ubuntu";

	private const string AddResponse =
		"<methodResponse><params><param><value><i4>0</i4></value></param></params></methodResponse>";

	private const string MultiCallResponse = """
		<methodResponse><params><param><value><array><data>
			<value><array><data>
				<value><string>ABCD</string></value>
				<value><string>Ubuntu.iso</string></value>
				<value><i8>2000</i8></value>
				<value><i8>500</i8></value>
				<value><i4>0</i4></value>
				<value><i4>1</i4></value>
				<value><string>/downloads</string></value>
				<value><string>movies</string></value>
			</data></array></value>
		</data></array></value></param></params></methodResponse>
		""";

	private readonly ITestOutputHelper _output;

	public RTorrentClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnMagnetHashAndSetLabel_WhenReleaseIsMagnet()
	{
		var (client, handler) = CreateClient("movies", (HttpStatusCode.OK, AddResponse));

		var id = await client.AddDownloadAsync(new ReleaseInfo { Title = "Ubuntu", Guid = "g", DownloadUrl = Magnet });

		Assert.Equal("ABCDEF1234567890ABCDEF1234567890ABCDEF12", id);
		Assert.Contains("<methodName>load.start</methodName>", handler.Bodies[0]);
		Assert.Contains("d.custom1.set=movies", handler.Bodies[0]);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapMultiCallRow_WhenRTorrentReturnsTorrents()
	{
		var (client, _) = CreateClient(null, (HttpStatusCode.OK, MultiCallResponse));

		var item = Assert.Single(await client.GetItemsAsync());

		Assert.Equal("ABCD", item.DownloadId);
		Assert.Equal("Ubuntu.iso", item.Title);
		Assert.Equal(2000, item.TotalSize);
		Assert.Equal(500, item.RemainingSize);
		Assert.Equal(DownloadItemStatus.DOWNLOADING, item.Status);
		Assert.Equal("/downloads", item.OutputPath);
		Assert.Equal("movies", item.Category);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldCallErase_WhenInvoked()
	{
		var (client, handler) = CreateClient(null,
			(HttpStatusCode.OK,
				"<methodResponse><params><param><value><i4>0</i4></value></param></params></methodResponse>"));

		await client.RemoveItemAsync("ABCD", true);

		Assert.Contains("<methodName>d.erase</methodName>", handler.Bodies[0]);
		Assert.Contains("ABCD", handler.Bodies[0]);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenServerRejects()
	{
		var (client, _) = CreateClient(null, (HttpStatusCode.InternalServerError, "boom"));

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync());
	}

	private (RTorrentClient Client, StubHandler Handler) CreateClient(string? category,
		params (HttpStatusCode Status, string Body)[] responses)
	{
		var handler = new StubHandler(responses);
		var settings = new RTorrentSettings { Host = "localhost", Port = 8080, Category = category };
		var client = new RTorrentClient(settings, new HttpClient(handler),
			new XunitLogger<RTorrentClient>(_output));

		return (client, handler);
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

		public StubHandler(params (HttpStatusCode Status, string Body)[] responses)
			=> _responses = new Queue<(HttpStatusCode, string)>(responses);

		public List<string> Bodies { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
			var (status, body) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();

			return new HttpResponseMessage(status) { Content = new StringContent(body) };
		}
	}
}
