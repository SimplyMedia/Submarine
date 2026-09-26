using Shouldly;
using Submarine.Core.Enums;
using Submarine.Core.Notifications;
using Xunit;

namespace Submarine.Infrastructure.Tests.Notifications;

/// <summary>
///     Validates the per type settings parsing and field errors.
/// </summary>
public sealed class NotificationSettingsJsonTests
{
	[Theory]
	[InlineData(NotificationType.DISCORD, """{"webhookUrl":"https://discord/app/1"}""")]
	[InlineData(NotificationType.TELEGRAM, """{"botToken":"123:abc","chatId":"42"}""")]
	[InlineData(NotificationType.WEBHOOK, """{"url":"https://hook","method":"PUT"}""")]
	[InlineData(NotificationType.SLACK, """{"webhookUrl":"https://hooks.slack.com/x"}""")]
	[InlineData(NotificationType.PUSHOVER, """{"apiKey":"a","userKey":"u"}""")]
	[InlineData(NotificationType.PUSHBULLET, """{"apiKey":"a"}""")]
	[InlineData(NotificationType.GOTIFY, """{"server":"https://gotify","appToken":"t"}""")]
	[InlineData(NotificationType.KODI, """{"host":"kodi"}""")]
	[InlineData(NotificationType.CUSTOM_SCRIPT, """{"path":"/tmp/run.sh"}""")]
	[InlineData(NotificationType.PLEX, """{"host":"plex","authToken":"t"}""")]
	[InlineData(NotificationType.EMBY, """{"host":"emby","apiKey":"k"}""")]
	[InlineData(NotificationType.JELLYFIN, """{"host":"jf","apiKey":"k"}""")]
	[InlineData(NotificationType.EMAIL, """{"server":"smtp","from":"a@b.c","to":["d@e.f"]}""")]
	[InlineData(NotificationType.NTFY, """{"topics":["t1"]}""")]
	[InlineData(NotificationType.APPRISE, """{"serverUrl":"https://apprise","statelessUrls":["json://x"]}""")]
	[InlineData(NotificationType.JOIN, """{"apiKey":"jk"}""")]
	[InlineData(NotificationType.MAILGUN, """{"apiKey":"mg","from":"a@b.c","senderDomain":"d.com","recipients":["e@f.g"]}""")]
	[InlineData(NotificationType.NOTIFIARR, """{"apiKey":"nr"}""")]
	[InlineData(NotificationType.PROWL, """{"apiKey":"pk"}""")]
	[InlineData(NotificationType.PUSHCUT, """{"notificationName":"Submarine","apiKey":"pc"}""")]
	[InlineData(NotificationType.PUSHSAFER, """{"apiKey":"ps"}""")]
	[InlineData(NotificationType.SENDGRID, """{"apiKey":"sg","from":"a@b.c","recipients":["d@e.f"]}""")]
	[InlineData(NotificationType.SIGNAL, """{"host":"signal","senderNumber":"1000","receiverId":"2000"}""")]
	[InlineData(NotificationType.SIMPLEPUSH, """{"key":"spk"}""")]
	[InlineData(NotificationType.SYNOLOGY_INDEXER, """{}""")]
	[InlineData(NotificationType.TWITTER, """{"consumerKey":"ck","consumerSecret":"cs","accessToken":"at","accessTokenSecret":"ats","directMessage":false}""")]
	[InlineData(NotificationType.TRAKT, """{"accessToken":"at","refreshToken":"rt","expiresAt":"2030-01-01T00:00:00"}""")]
	public void Validate_ShouldPass_WhenRequiredFieldsAreSet(NotificationType type, string json)
	{
		var result = NotificationSettingsJson.Validate(type, json);

		result.IsValid.ShouldBeTrue(string.Join(";", result.Errors.SelectMany(e => e.Value)));
		result.Settings.ShouldNotBeNull();
	}

	[Fact]
	public void Validate_ShouldRequireWebhookUrl_ForDiscord()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.DISCORD, "{}");

		result.IsValid.ShouldBeFalse();
		result.Errors["webhookUrl"].ShouldContain("is required");
	}

	[Fact]
	public void Validate_ShouldRejectBadMethod_ForWebhook()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.WEBHOOK, """{"url":"https://hook","method":"DELETE"}""");

		result.IsValid.ShouldBeFalse();
		result.Errors["method"].ShouldContain("must be POST or PUT");
	}

	[Fact]
	public void Validate_ShouldRejectOutOfRangePriority_ForPushover()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.PUSHOVER, """{"apiKey":"a","userKey":"u","priority":5}""");

		result.IsValid.ShouldBeFalse();
		result.Errors["priority"].ShouldContain("must be between -2 and 2");
	}

	[Fact]
	public void Validate_ShouldRequireTopics_ForNtfy()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.NTFY, "{}");

		result.IsValid.ShouldBeFalse();
		result.Errors["topics"].ShouldContain("must not be empty");
	}

	[Fact]
	public void Validate_ShouldRequireServerUrlAndTargets_ForApprise()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.APPRISE, """{"serverUrl":"https://apprise"}""");

		result.IsValid.ShouldBeFalse();
		result.Errors["statelessUrls"].ShouldContain("must not be empty when no configuration key is set");
	}

	[Fact]
	public void Validate_ShouldRequireSenderDomainAndRecipients_ForMailgun()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.MAILGUN, """{"apiKey":"k","from":"a@b.c"}""");

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContainKey("senderDomain");
		result.Errors.ShouldContainKey("recipients");
	}

	[Fact]
	public void Validate_ShouldRequireRetryAndExpireInRange_ForPushsaferEmergency()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.PUSHSAFER, """{"apiKey":"k","priority":2,"retry":10,"expire":20000}""");

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContainKey("retry");
		result.Errors.ShouldContainKey("expire");
	}

	[Fact]
	public void Validate_ShouldNotRequireRetryAndExpire_ForPushsaferNonEmergency()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.PUSHSAFER, """{"apiKey":"k","priority":0}""");

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ShouldRequireMention_ForTwitterDirectMessage()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.TWITTER,
			"""{"consumerKey":"ck","consumerSecret":"cs","accessToken":"at","accessTokenSecret":"ats","directMessage":true}""");

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContainKey("mention");
	}

	[Fact]
	public void Validate_ShouldReportJsonLevelErrors()
	{
		var result = NotificationSettingsJson.Validate(NotificationType.SLACK, "{not json");

		result.IsValid.ShouldBeFalse();
		result.Errors["$"].ShouldNotBeEmpty();
		result.Settings.ShouldBeNull();
	}

	[Fact]
	public void Parse_ShouldThrow_WhenInvalid()
	{
		Should.Throw<InvalidOperationException>(
			() => NotificationSettingsJson.Parse(NotificationType.PLEX, "{}"))
			.Message.ShouldContain("authToken");
	}

	[Fact]
	public void Serialize_ShouldRoundTripEnumsAsStrings()
	{
		var settings = new EmailSettings
		{
			Server = "smtp.example.com",
			From = "submarine@example.com",
			To = ["me@example.com"],
			UseEncryption = EmailSettings.EncryptionType.SSL
		};

		var json = NotificationSettingsJson.Serialize(settings);

		json.ShouldContain("\"useEncryption\":\"SSL\"");
		var parsed = (EmailSettings)NotificationSettingsJson.Parse(NotificationType.EMAIL, json);
		parsed.UseEncryption.ShouldBe(EmailSettings.EncryptionType.SSL);
		parsed.To.ShouldContain("me@example.com");
	}

	[Fact]
	public void Describe_ShouldMarkRequiredFields_AndListOptions()
	{
		var fields = NotificationSettingsJson.Describe(NotificationType.DISCORD);

		var webhook = fields.Single(f => f.Name == "webhookUrl");
		webhook.Required.ShouldBeTrue();
		webhook.Type.ShouldBe("url");

		var grabFields = fields.Single(f => f.Name == "grabFields");
		grabFields.Type.ShouldBe("tags");
		grabFields.Options.ShouldNotBeNull();
		grabFields.Options.ShouldContain("quality");
	}

	[Fact]
	public void DescribeAll_ShouldCoverEveryType()
	{
		var all = NotificationSettingsJson.DescribeAll();

		all.Count.ShouldBe(Enum.GetValues<NotificationType>().Length);
		all.Values.ShouldNotContain(fields => fields.Length == 0);
	}
}
