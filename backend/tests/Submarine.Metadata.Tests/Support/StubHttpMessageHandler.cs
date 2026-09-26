using System.Net;
using System.Text;

namespace Submarine.Metadata.Tests.Support;

/// <summary>
///     Queue-based stub handler: responses are consumed in order, the last
///     one repeats. All requests are recorded for assertions.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
	private readonly List<(HttpStatusCode StatusCode, string Content)> _responses = [];
	private readonly List<HttpRequestMessage> _requests = [];

	public IReadOnlyList<HttpRequestMessage> Requests => _requests;

	public IReadOnlyList<string> RequestPaths => _requests.Select(r => r.RequestUri!.PathAndQuery).ToList();

	public StubHttpMessageHandler Respond(HttpStatusCode statusCode, string json)
	{
		_responses.Add((statusCode, json));
		return this;
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		_requests.Add(request);
		if (_responses.Count == 0)
			throw new InvalidOperationException($"No stubbed response registered for {request.Method} {request.RequestUri}.");
		var (statusCode, content) = _responses[0];
		if (_responses.Count > 1)
			_responses.RemoveAt(0);
		return Task.FromResult(new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(content, Encoding.UTF8, "application/json")
		});
	}
}
