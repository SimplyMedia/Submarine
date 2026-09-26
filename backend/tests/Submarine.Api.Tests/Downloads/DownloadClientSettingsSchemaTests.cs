using Shouldly;
using Submarine.Api.Features.DownloadClients;
using Submarine.Core.Download;
using Xunit;

namespace Submarine.Api.Tests.Downloads;

public sealed class DownloadClientSettingsSchemaTests
{
	[Theory]
	[InlineData(DownloadClientType.QBITTORRENT)]
	[InlineData(DownloadClientType.TRANSMISSION)]
	[InlineData(DownloadClientType.DELUGE)]
	[InlineData(DownloadClientType.RTORRENT)]
	[InlineData(DownloadClientType.UTORRENT)]
	[InlineData(DownloadClientType.ARIA2)]
	[InlineData(DownloadClientType.FLOOD)]
	[InlineData(DownloadClientType.DOWNLOAD_STATION)]
	[InlineData(DownloadClientType.SABNZBD)]
	[InlineData(DownloadClientType.NZBGET)]
	[InlineData(DownloadClientType.TORRENT_BLACKHOLE)]
	[InlineData(DownloadClientType.USENET_BLACKHOLE)]
	public void FieldsOf_ShouldReturnNonEmptyFields_ForEveryClientType(DownloadClientType type)
		=> DownloadClientSettingsSchema.FieldsOf(type).ShouldNotBeEmpty();

	[Fact]
	public void FieldsOf_ShouldMarkHost_AsRequired_ForQBittorrent()
	{
		var fields = DownloadClientSettingsSchema.FieldsOf(DownloadClientType.QBITTORRENT);

		var host = fields.Single(x => x.Name == "host");
		host.Required.ShouldBeTrue();
		host.ClrType.ShouldBe("string");
	}

	[Fact]
	public void FieldsOf_ShouldMarkUsername_AsOptional_ForQBittorrent()
	{
		var fields = DownloadClientSettingsSchema.FieldsOf(DownloadClientType.QBITTORRENT);

		var username = fields.Single(x => x.Name == "username");
		username.Required.ShouldBeFalse();
	}

	[Fact]
	public void FieldsOf_ShouldExposeEnumValues_ForEnumFields()
	{
		var fields = DownloadClientSettingsSchema.FieldsOf(DownloadClientType.QBITTORRENT);

		var initialState = fields.Single(x => x.Name == "initialState");
		initialState.ClrType.ShouldBe("enum");
		initialState.EnumValues.ShouldNotBeNull();
		initialState.EnumValues.ShouldContain("START");
		initialState.EnumValues.ShouldContain("FORCE_START");
		initialState.EnumValues.ShouldContain("PAUSE");
	}

	[Fact]
	public void FieldsOf_ShouldReportCorrectClrTypes_ForIntAndBoolFields()
	{
		var fields = DownloadClientSettingsSchema.FieldsOf(DownloadClientType.QBITTORRENT);

		fields.Single(x => x.Name == "port").ClrType.ShouldBe("int");
		fields.Single(x => x.Name == "useSsl").ClrType.ShouldBe("bool");
	}

	[Fact]
	public void FieldsOf_ShouldCamelCaseFieldNames()
		=> DownloadClientSettingsSchema.FieldsOf(DownloadClientType.QBITTORRENT)
			.ShouldAllBe(x => x.Name.Length == 0 || char.IsLower(x.Name[0]));

	[Fact]
	public void FieldsOf_ShouldIncludeDefaultValues()
	{
		var fields = DownloadClientSettingsSchema.FieldsOf(DownloadClientType.QBITTORRENT);

		fields.Single(x => x.Name == "port").Default.ShouldBe(8080);
	}
}
