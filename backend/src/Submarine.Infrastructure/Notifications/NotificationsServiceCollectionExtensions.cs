using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Dependency injection registration for the notification stack.
/// </summary>
public static class NotificationsServiceCollectionExtensions
{
	/// <summary>
	///     Registers the notification senders, the factory and the dispatcher.
	/// </summary>
	public static IServiceCollection AddSubmarineNotifications(this IServiceCollection services)
	{
		services.AddHttpClient(NotificationSenderFactory.HttpClientName,
			client => client.Timeout = NotificationSenderFactory.HttpTimeout);
		services.AddSingleton<INotificationSender, DiscordSender>();
		services.AddSingleton<INotificationSender, TelegramSender>();
		services.AddSingleton<INotificationSender, WebhookSender>();
		services.AddSingleton<INotificationSender, SlackSender>();
		services.AddSingleton<INotificationSender, PushoverSender>();
		services.AddSingleton<INotificationSender, PushbulletSender>();
		services.AddSingleton<INotificationSender, GotifySender>();
		services.AddSingleton<INotificationSender, KodiSender>();
		services.AddSingleton<INotificationSender, CustomScriptSender>();
		services.AddSingleton<INotificationSender, PlexSender>();
		services.AddSingleton<INotificationSender, EmbySender>();
		services.AddSingleton<INotificationSender, JellyfinSender>();
		services.AddSingleton<INotificationSender, EmailSender>();
		services.AddSingleton<INotificationSender, NtfySender>();
		services.AddSingleton<INotificationSender, AppriseSender>();
		services.AddSingleton<INotificationSender, JoinSender>();
		services.AddSingleton<INotificationSender, MailgunSender>();
		services.AddSingleton<INotificationSender, NotifiarrSender>();
		services.AddSingleton<INotificationSender, ProwlSender>();
		services.AddSingleton<INotificationSender, PushcutSender>();
		services.AddSingleton<INotificationSender, PushsaferSender>();
		services.AddSingleton<INotificationSender, SendGridSender>();
		services.AddSingleton<INotificationSender, SignalSender>();
		services.AddSingleton<INotificationSender, SimplepushSender>();
		services.AddSingleton<ISynologyIndexerProcess, SynologyIndexerProcess>();
		services.AddSingleton<INotificationSender, SynologyIndexerSender>();
		services.AddSingleton<INotificationSender, TwitterSender>();
		services.AddScoped<INotificationSender, TraktSender>();
		services.AddSingleton<ITraktAuthService, TraktAuthService>();
		services.AddScoped<ITraktTokenRefresher, TraktTokenRefresher>();
		services.AddScoped<INotificationSenderFactory, NotificationSenderFactory>();
		services.AddScoped<INotificationStatusService, NotificationStatusService>();
		services.AddScoped<NotificationDispatcher>();

		return services;
	}
}
