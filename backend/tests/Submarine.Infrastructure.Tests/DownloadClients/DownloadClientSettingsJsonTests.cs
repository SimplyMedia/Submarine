using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class DownloadClientSettingsJsonTests
{
	private const string ValidJson = """{"host":"x.local","port":1234,"useSsl":true}""";

	[Fact]
	public void Validate_ShouldReportMissingRequiredFields()
	{
		var result = DownloadClientSettingsJson.Validate(DownloadClientType.DELUGE, "{}");

		result.IsValid.ShouldBeFalse();
		result.Errors["host"].ShouldContain("is required");
		result.Errors["password"].ShouldContain("is required");
		result.Settings.ShouldNotBeNull();
	}

	[Fact]
	public void Validate_ShouldReportPortOutOfRange()
	{
		var result = DownloadClientSettingsJson.Validate(DownloadClientType.QBITTORRENT,
			"""{"host":"x","port":0}""");

		result.IsValid.ShouldBeFalse();
		result.Errors["port"].ShouldContain("must be between 1 and 65535");
	}

	[Fact]
	public void Validate_ShouldReportJsonError_WhenJsonMalformed()
	{
		var result = DownloadClientSettingsJson.Validate(DownloadClientType.QBITTORRENT, "{nope");

		result.IsValid.ShouldBeFalse();
		result.Settings.ShouldBeNull();
		result.Errors["$"].ShouldNotBeNull();
	}

	[Fact]
	public void Validate_ShouldReportMissingRequiredFieldsPerType()
	{
		var cases = new Dictionary<DownloadClientType, string[]>
		{
			[DownloadClientType.QBITTORRENT] = ["host"],
			[DownloadClientType.TRANSMISSION] = ["host"],
			[DownloadClientType.DELUGE] = ["host", "password"],
			[DownloadClientType.RTORRENT] = ["host"],
			[DownloadClientType.UTORRENT] = ["host"],
			[DownloadClientType.ARIA2] = ["host"],
			[DownloadClientType.FLOOD] = ["host", "username", "password"],
			[DownloadClientType.DOWNLOAD_STATION] = ["host", "username", "password"],
			[DownloadClientType.SABNZBD] = ["host", "apiKey"],
			[DownloadClientType.NZBGET] = ["host", "username", "password"],
			[DownloadClientType.TORRENT_BLACKHOLE] = ["torrentFolder", "watchFolder"],
			[DownloadClientType.USENET_BLACKHOLE] = ["nzbFolder", "watchFolder"],
			[DownloadClientType.HADOUKEN] = ["host", "username", "password"],
			[DownloadClientType.NZBVORTEX] = ["host", "apiKey"],
			[DownloadClientType.PNEUMATIC] = ["nzbFolder", "strmFolder"],
			[DownloadClientType.FREEBOX_DOWNLOAD] = ["appId", "appToken"],
			[DownloadClientType.RQBIT] = ["host"]
		};

		foreach (var (type, expectedFields) in cases)
		{
			var result = DownloadClientSettingsJson.Validate(type, "{}");
			result.IsValid.ShouldBeFalse($"{type} should require {string.Join(",", expectedFields)}");

			foreach (var field in expectedFields)
				result.Errors.ShouldContainKey(field, $"{type} should report {field}");
		}
	}

	[Fact]
	public void Parse_ShouldDeserializeDefaultsAndEnums()
	{
		var settings = (QBittorrentSettings)DownloadClientSettingsJson.Parse(DownloadClientType.QBITTORRENT,
			"""{"host":"qb.local"}""");

		settings.Host.ShouldBe("qb.local");
		settings.Port.ShouldBe(8080);
		settings.UseSsl.ShouldBeFalse();
		settings.RecentPriority.ShouldBe(DownloadClientItemPriority.FIRST);
		settings.OlderPriority.ShouldBe(DownloadClientItemPriority.LAST);
		settings.InitialState.ShouldBe(QBittorrentInitialState.START);
	}

	[Fact]
	public void Parse_ShouldAcceptEnumNamesAsStrings()
	{
		var settings = (SabnzbdSettings)DownloadClientSettingsJson.Parse(DownloadClientType.SABNZBD,
			"""{"host":"sab.local","apiKey":"key","recentPriority":"HIGH","olderPriority":"LOW"}""");

		settings.RecentPriority.ShouldBe(DownloadClientPriority.HIGH);
		settings.OlderPriority.ShouldBe(DownloadClientPriority.LOW);
	}

	[Fact]
	public void Parse_ShouldThrow_WhenRequiredFieldsMissing()
	{
		Should.Throw<DownloadClientException>(() =>
			DownloadClientSettingsJson.Parse(DownloadClientType.NZBGET, "{}")).Message.ShouldContain("Invalid");
	}

	[Fact]
	public void Serialize_ShouldRoundTripSettings()
	{
		var settings = new FloodSettings
		{
			Host = "flood.local",
			Username = "u",
			Password = "p",
			Tags = ["tv", "anime"],
			AddPaused = true
		};

		var json = DownloadClientSettingsJson.Serialize(settings);
		var parsed = (FloodSettings)DownloadClientSettingsJson.Parse(DownloadClientType.FLOOD, json);

		parsed.Host.ShouldBe(settings.Host);
		parsed.Username.ShouldBe(settings.Username);
		parsed.Password.ShouldBe(settings.Password);
		parsed.Tags.ShouldBe(settings.Tags);
		parsed.AddPaused.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ShouldAcceptValidMinimalSettings()
	{
		var result = DownloadClientSettingsJson.Validate(DownloadClientType.QBITTORRENT, ValidJson);

		result.IsValid.ShouldBeTrue();
		result.Settings.ShouldBeOfType<QBittorrentSettings>();
	}
}
