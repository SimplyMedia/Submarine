using System.Net;
using System.Text;

namespace Submarine.Metadata.Tests.Clients;

internal class StubHttpMessageHandler : HttpMessageHandler
{
	private readonly string _responseJson;

	public StubHttpMessageHandler(string responseJson)
		=> _responseJson = responseJson;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
		CancellationToken cancellationToken)
		=> Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
		});
}
