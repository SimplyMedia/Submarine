using System.Text.Json;
using Shouldly;
using Submarine.Api.Features.DownloadClients;
using Submarine.Api.Features.LibraryImport;
using Submarine.Api.Features.ManualImport;
using Submarine.Api.Features.Queue;
using Submarine.Api.Features.Rename;
using Submarine.Core.Download;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.Tests.Downloads;

public sealed class DownloadsValidatorsTests
{
	[Fact]
	public async Task DownloadClientBulkRequestValidator_ShouldFail_WhenTagModeIsInvalid()
	{
		var validator = new DownloadClientBulkRequestValidator();
		var result = await validator.ValidateAsync(new DownloadClientBulkRequest([1], null, null, new TagChangeRequest("invalid", [1])));

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task DownloadClientBulkRequestValidator_ShouldPass_WhenTagModeIsValid()
	{
		var validator = new DownloadClientBulkRequestValidator();
		var result = await validator.ValidateAsync(new DownloadClientBulkRequest([1], true, 5, new TagChangeRequest("add", [1])));

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task DownloadClientRequestValidator_ShouldFail_WhenNameEmpty()
	{
		var validator = new DownloadClientRequestValidator();
		var result = await validator.ValidateAsync(new DownloadClientRequest(string.Empty, DownloadClientType.QBITTORRENT, true, 1, JsonSerializer.Deserialize<JsonElement>("{}"), false, false, null));

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ManualImportRequestValidator_ShouldFail_WhenNeitherSeriesIdNorMovieIdGiven()
	{
		var validator = new ManualImportRequestValidator();
		var item = new ManualImportRequestItem("/path/file.mkv", null, null, null, 1, null, null, null, null);
		var result = await validator.ValidateAsync(new ManualImportRequest([item], "move"));

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ManualImportRequestValidator_ShouldPass_WhenSeriesIdGiven()
	{
		var validator = new ManualImportRequestValidator();
		var item = new ManualImportRequestItem("/path/file.mkv", 1, [2], null, 1, null, null, null, null);
		var result = await validator.ValidateAsync(new ManualImportRequest([item], "move"));

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ManualImportRequestValidator_ShouldFail_WhenImportModeInvalid()
	{
		var validator = new ManualImportRequestValidator();
		var item = new ManualImportRequestItem("/path/file.mkv", 1, [2], null, 1, null, null, null, null);
		var result = await validator.ValidateAsync(new ManualImportRequest([item], "delete"));

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task LibraryImportAddRequestValidator_ShouldFail_WhenNeitherTvdbNorTmdbGiven()
	{
		var validator = new LibraryImportAddRequestValidator();
		var item = new LibraryImportAddItem(1, "Some Folder", null, null, 1, 1, null, null);
		var result = await validator.ValidateAsync(new LibraryImportAddRequest([item]));

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task LibraryImportAddRequestValidator_ShouldPass_WhenTvdbIdGiven()
	{
		var validator = new LibraryImportAddRequestValidator();
		var item = new LibraryImportAddItem(1, "Some Folder", 12345, null, 1, 1, SeriesType.STANDARD, true);
		var result = await validator.ValidateAsync(new LibraryImportAddRequest([item]));

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task RenameRequestValidator_ShouldFail_WhenNeitherSeriesIdNorMovieIdGiven()
	{
		var validator = new RenameRequestValidator();
		var result = await validator.ValidateAsync(new RenameRequest(null, null, null));

		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task RenameRequestValidator_ShouldPass_WhenMovieIdGiven()
	{
		var validator = new RenameRequestValidator();
		var result = await validator.ValidateAsync(new RenameRequest(null, 7, null));

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task BulkQueueDeleteRequestValidator_ShouldFail_WhenIdsEmpty()
	{
		var validator = new BulkQueueDeleteRequestValidator();
		var result = await validator.ValidateAsync(new BulkQueueDeleteRequest([], null, null, null));

		result.IsValid.ShouldBeFalse();
	}
}
