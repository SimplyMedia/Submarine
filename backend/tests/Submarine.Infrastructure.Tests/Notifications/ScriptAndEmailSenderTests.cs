using Shouldly;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Notifications;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Notifications;
using Xunit;

namespace Submarine.Infrastructure.Tests.Notifications;

/// <summary>
///     Asserts mail construction of the email sender and the custom script process setup.
/// </summary>
public sealed class ScriptAndEmailSenderTests
{
	private static NotificationMessage Message()
		=> new(
			NotificationEventType.GRAB,
			"Grabbed",
			"Some Show - S01E01 [WEBDL-1080p]",
			5,
			null,
			"Some Show",
			2024,
			new QualityModel(new QualityResolutionModel(), new Revision()),
			[Language.ENGLISH, Language.GERMAN],
			"GRP",
			"idx",
			"qb",
			"dl-1",
			2_000,
			"/media/tv/x.mkv",
			null,
			[],
			[]);

	[Fact]
	public void Email_BuildMimeMessage_ShouldSetAddressesAndSubject()
	{
		var settings = new EmailSettings
		{
			From = "submarine@example.com",
			To = ["one@example.com", "two@example.com"],
			Cc = ["cc@example.com"],
			Bcc = ["bcc@example.com"]
		};

		var mime = EmailSender.BuildMimeMessage(Message(), settings);

		mime.From.Single().ToString().ShouldBe("submarine@example.com");
		mime.To.Select(x => x.ToString()).ShouldBe(["one@example.com", "two@example.com"]);
		mime.Cc.Select(x => x.ToString()).ShouldBe(["cc@example.com"]);
		mime.Bcc.Select(x => x.ToString()).ShouldBe(["bcc@example.com"]);
		mime.Subject.ShouldBe("Grabbed");
		var text = ((MimeKit.TextPart)mime.Body!).Text;
		text.ShouldBe("Some Show - S01E01 [WEBDL-1080p]");
	}

	[Fact]
	public void CustomScript_BuildStartInfo_ShouldSetEnvironmentVariables()
	{
		var sender = new CustomScriptSender(
			Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomScriptSender>.Instance);

		var startInfo = sender.BuildStartInfo(
			new CustomScriptSettings { Path = "/tmp/run.sh", Arguments = "--verbose" },
			Message());

		startInfo.FileName.ShouldBe("/tmp/run.sh");
		startInfo.ArgumentList.ShouldContain("--verbose");
		startInfo.Environment["submarine_eventtype"]!.ShouldBe("GRAB");
		startInfo.Environment["submarine_series_id"]!.ShouldBe("5");
		startInfo.Environment["submarine_movie_id"]!.ShouldBe(string.Empty);
		startInfo.Environment["submarine_media_title"]!.ShouldBe("Some Show");
		startInfo.Environment["submarine_year"]!.ShouldBe("2024");
		startInfo.Environment["submarine_releasegroup"]!.ShouldBe("GRP");
		startInfo.Environment["submarine_indexer"]!.ShouldBe("idx");
		startInfo.Environment["submarine_downloadclient"]!.ShouldBe("qb");
		startInfo.Environment["submarine_downloadid"]!.ShouldBe("dl-1");
		startInfo.Environment["submarine_size"]!.ShouldBe("2000");
		startInfo.Environment["submarine_path"]!.ShouldBe("/media/tv/x.mkv");
		startInfo.Environment["submarine_languages"]!.ShouldContain("ENGLISH");
	}

	[Fact]
	public async Task CustomScript_RunAsync_ShouldComplete_OnZeroExit()
	{
		var scriptPath = Path.Combine(Path.GetTempPath(), $"submarine-test-{Guid.NewGuid():N}.sh");
		await File.WriteAllTextAsync(scriptPath, "#!/bin/sh\nexit 0\n", TestContext.Current.CancellationToken);
		if (!OperatingSystem.IsWindows())
		{
			File.SetUnixFileMode(scriptPath, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
		}
		try
		{
			var sender = new CustomScriptSender(
				Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomScriptSender>.Instance);
			var startInfo = sender.BuildStartInfo(
				new CustomScriptSettings { Path = scriptPath },
				Message());

			await sender.RunAsync(startInfo, TestContext.Current.CancellationToken);
		}
		finally
		{
			File.Delete(scriptPath);
		}
	}
}
