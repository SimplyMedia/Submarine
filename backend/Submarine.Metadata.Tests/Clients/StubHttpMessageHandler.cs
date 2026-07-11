using System.Net;
using System.Text;

namespace Submarine.Metadata.Tests.Clients;

internal class StubHttpMessageHandler : HttpMessageHandler
{
	private readonly Queue<(HttpStatusCode Status, string Json)> _responses;

	public List<HttpRequestMessage> Requests { get; } = [];

	public StubHttpMessageHandler(string responseJson)
		: this((HttpStatusCode.OK, responseJson))
	{
	}

	public StubHttpMessageHandler(params (HttpStatusCode Status, string Json)[] responses)
		=> _responses = new Queue<(HttpStatusCode, string)>(responses);

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		Requests.Add(request);

		var (status, json) = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();

		return Task.FromResult(new HttpResponseMessage(status)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		});
	}
}
