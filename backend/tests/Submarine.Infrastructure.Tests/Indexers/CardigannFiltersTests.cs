using System;
using System.Collections.Generic;
using System.Globalization;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class CardigannFiltersTests
{
	private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

	[Theory]
	[InlineData("prepend", "world", new[] { "hello " }, "hello world")]
	[InlineData("append", "hello", new[] { " world" }, "hello world")]
	[InlineData("tolower", "HeLLo", null, "hello")]
	[InlineData("toupper", "hello", null, "HELLO")]
	[InlineData("trim", "  x  ", null, "x")]
	public void Apply_ShouldTransformStrings_WhenBasicFiltersUsed(string name, string input, string[]? args, string expected)
		=> CardigannFilters.Apply(name, input, args ?? [], Now).ShouldBe(expected);

	[Fact]
	public void Apply_ShouldReplace_WhenReplaceUsed()
		=> CardigannFilters.Apply("replace", "a-b-c", ["-", "+"], Now).ShouldBe("a+b+c");

	[Fact]
	public void Apply_ShouldSplitAndIndex_WhenSplitUsed()
		=> CardigannFilters.Apply("split", "the/boys/s03", ["/", "1"], Now).ShouldBe("boys");

	[Fact]
	public void Apply_ShouldTrimChars_WhenCharsProvided()
		=> CardigannFilters.Apply("trim", "xxhelloxx", ["x"], Now).ShouldBe("hello");

	[Fact]
	public void Apply_ShouldExtractFirstGroup_WhenRegexpMatches()
		=> CardigannFilters.Apply("regexp", "/detail/some-title-torrent-123.html", ["/(.+?)-torrent-\\d+\\.html"], Now)
			.ShouldBe("detail/some-title");

	[Fact]
	public void Apply_ShouldExtractWholeMatch_WhenNoGroups()
		=> CardigannFilters.Apply("regexp", "size: 1.5 GB", ["\\d+\\.\\d+ [KMG]B"], Now)
			.ShouldBe("1.5 GB");

	[Fact]
	public void Apply_ShouldPreferGroup_WhenRegexpHasOne()
		=> CardigannFilters.Apply("regexp", "size: 1.5 GB", ["size: (\\d+\\.\\d+ [KMG]B)"], Now)
			.ShouldBe("1.5 GB");

	[Fact]
	public void Apply_ShouldReturnEmpty_WhenRegexpDoesNotMatch()
		=> CardigannFilters.Apply("regexp", "abc", ["\\d+"], Now).ShouldBeEmpty();

	[Fact]
	public void Apply_ShouldRegexReplace_WhenReReplaceUsed()
		=> CardigannFilters.Apply("re_replace", "The Boys S03 1080p", ["S0?(\\d+)", "Season $1"], Now)
			.ShouldBe("The Boys Season 3 1080p");

	[Theory]
	[InlineData("2026-09-26 14:30:00 +02:00", "yyyy-MM-dd HH:mm:ss zzz", "2026-09-26T12:30:00.0000000Z")]
	[InlineData("7am Sep. 14th", "htt MMM. d", "2026-09-14T07:00:00.0000000Z")]
	[InlineData("Apr 18 11", "MMM. d yy", "2011-04-18T00:00:00.0000000Z")]
	public void Apply_ShouldParseDates_WhenDateparseUsed(string input, string format, string expectedUtc)
		=> CardigannFilters.Apply("dateparse", input, [format], Now)
			.ShouldBe(expectedUtc);

	[Fact]
	public void Apply_ShouldParseDatesWithWeekday_WhenDddUsed()
		=> CardigannFilters.Apply("dateparse", "Sun, 18 Jan 2026 04:05:41 +0000", ["ddd, dd MMM yyyy HH:mm:ss zzz"], Now)
			.ShouldBe("2026-01-18T04:05:41.0000000Z");

	[Fact]
	public void Apply_ShouldReturnEmpty_WhenDateparseDoesNotMatch()
		=> CardigannFilters.Apply("dateparse", "garbage", ["yyyy-MM-dd"], Now).ShouldBeEmpty();

	[Fact]
	public void Apply_ShouldParseTimeWithOffset_WhenTimeparseUsed()
		=> CardigannFilters.Apply("timeparse", "12:25 +02:00", ["HH:mm zzz"], Now)
			.ShouldBe("2026-09-26T10:25:00.0000000Z");

	[Theory]
	[InlineData("2 days ago", "2026-09-24T12:00:00.0000000Z")]
	[InlineData("3 hours", "2026-09-26T09:00:00.0000000Z")]
	[InlineData("1w 2d", "2026-09-17T12:00:00.0000000Z")]
	public void Apply_ShouldParseRelativeTimes_WhenTimeagoUsed(string input, string expectedUtc)
		=> CardigannFilters.Apply("timeago", input, [], Now)
			.ShouldBe(expectedUtc);

	[Fact]
	public void Apply_ShouldUseTodayAtTime_WhenFuzzytimeUsed()
		=> CardigannFilters.Apply("fuzzytime", "7:25am", [], Now)
			.ShouldBe("2026-09-26T07:25:00.0000000Z");

	[Fact]
	public void Apply_ShouldParseYesterday_WhenFuzzytimeUsed()
		=> CardigannFilters.Apply("fuzzytime", "yesterday", [], Now)
			.ShouldBe("2026-09-25T12:00:00.0000000Z");

	[Fact]
	public void Apply_ShouldExtractQueryParam_WhenQuerystringUsed()
		=> CardigannFilters.Apply("querystring", "/search.php?c=7&page=2", ["c"], Now).ShouldBe("7");

	[Fact]
	public void Apply_ShouldStripInvalidFilenameChars_WhenValidfilenameUsed()
	{
		CardigannFilters.Apply("validfilename", "bad/name", [], Now).ShouldBe("badname");
		CardigannFilters.Apply("validfilename", "bad/name", ["-"], Now).ShouldBe("bad-name");
	}

	[Fact]
	public void Apply_ShouldRemoveDiacritics_WhenDiacriticsUsed()
		=> CardigannFilters.Apply("diacritics", "cliché café", [], Now).ShouldBe("cliche cafe");

	[Fact]
	public void Apply_ShouldJoinJsonArrayProperty_WhenJsonjoinarrayUsed()
	{
		const string json = """[{"name":"a"},{"name":"b"}]""";
		CardigannFilters.Apply("jsonjoinarray", json, ["name", ","], Now).ShouldBe("a,b");
	}

	[Fact]
	public void Apply_ShouldHexDumpUtf8Bytes_WhenHexdumpUsed()
		=> CardigannFilters.Apply("hexdump", "AB", [], Now).ShouldBe("4142");

	[Fact]
	public void Apply_ShouldQuoteAndEscape_WhenStrdumpUsed()
		=> CardigannFilters.Apply("strdump", "a\"b\nc", [], Now).ShouldBe("\"a\\\"b\\nc\"");

	[Fact]
	public void Apply_ShouldPassThrough_WhenValidateMatches()
		=> CardigannFilters.Apply("validate", "ok", ["ok"], Now).ShouldBe("ok");

	[Fact]
	public void Apply_ShouldThrow_WhenValidateFails()
		=> Should.Throw<IndexerException>(() => CardigannFilters.Apply("validate", "no", ["ok"], Now));

	[Fact]
	public void Apply_ShouldHtmlDecode_WhenHtmldecodeUsed()
		=> CardigannFilters.Apply("htmldecode", "a &amp; b &lt;c&gt;", [], Now).ShouldBe("a & b <c>");

	[Fact]
	public void Apply_ShouldUrlDecodeAndEncode_WhenUrlFiltersUsed()
	{
		CardigannFilters.Apply("urldecode", "the%20boys", [], Now).ShouldBe("the boys");
		CardigannFilters.Apply("urlencode", "the boys", [], Now).ShouldBe("the+boys");
	}

	[Fact]
	public void Apply_ShouldThrow_WhenFilterUnknown()
		=> Should.Throw<IndexerException>(() => CardigannFilters.Apply("nonsense", "x", [], Now));
}

public class CardigannSizeParserTests
{
	[Theory]
	[InlineData("1.4 GB", 1503238554)]
	[InlineData("512 MB", 536870912)]
	[InlineData("700 MiB", 734003200)]
	[InlineData("123456789", 123456789)]
	[InlineData("2.5 KB", 2560)]
	[InlineData("1 TB", 1099511627776)]
	public void ParseBytes_ShouldParseSizes_WhenUnitPresent(string input, long expected)
		=> CardigannSizeParser.ParseBytes(input).ShouldBe(expected);

	[Fact]
	public void ParseBytes_ShouldReturnNull_WhenUnparsable()
		=> CardigannSizeParser.ParseBytes("not a size").ShouldBeNull();
}

public class CardigannTimeAgoParserTests
{
	private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Parse_ShouldCombineUnits_WhenTextHasMultipleUnits()
		=> CardigannTimeAgoParser.Parse("1 month 2 days", Now)
			.ShouldBe(new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc));

	[Fact]
	public void ParseFuzzy_ShouldHandleToday_WhenTextSaysToday()
		=> CardigannTimeAgoParser.ParseFuzzy("today", Now).ShouldBe(Now);
}
