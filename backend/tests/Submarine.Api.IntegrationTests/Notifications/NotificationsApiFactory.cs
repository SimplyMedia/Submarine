using Microsoft.Extensions.DependencyInjection;
using Submarine.Api.IntegrationTests.Library;
using Submarine.Infrastructure.Notifications;

namespace Submarine.Api.IntegrationTests.Notifications;

/// <summary>
///     Hosts the real API against a temporary Sqlite database with the shared notification HttpClient
///     substituted so outbound sender calls hit a stub instead of the real internet.
/// </summary>
public sealed class NotificationsApiFactory : SubmarineApiFactory
{
	/// <summary>Stub handler backing every request the notification senders make. Defaults to a 200 JSON response.</summary>
	public StubHttpHandler HttpHandler { get; set; } = new("""{}""");

	/// <inheritdoc />
	protected override void ConfigureTestServices(IServiceCollection services)
	{
		services.AddHttpClient(NotificationSenderFactory.HttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => HttpHandler);
	}
}
