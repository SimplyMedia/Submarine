using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class CardigannSelectorEngineTests
{
	private const string Html = """
		<html><body>
			<table>
				<tr class="row"><td class="name"><a href="/d/1">Alpha</a></td><td class="cat">Movies</td><td class="size">1.4 GB</td><td class="extra"><span>noise</span>meta</td></tr>
				<tr class="row"><td class="name"><a href="/d/2">Beta</a></td><td class="cat">TV</td><td class="size">512 MB</td><td class="extra">plain</td></tr>
				<tr class="row hidden"><td class="name"><a href="/d/3">Gamma</a></td><td class="cat">TV</td><td class="size">2 GB</td></tr>
			</table>
			<div id="detail"><a href="magnet:?xt=urn:btih:0123456789012345678901234567890123456789&amp;dn=x">magnet</a></div>
		</body></html>
		""";

	private const string Json = """
		{"data": {"movies": [
			{"title": "Alpha", "torrents": [{"quality": "720p", "hash": "aaa", "size_bytes": 1400000000}], "year": 2020},
			{"title": "Beta", "torrents": [{"quality": "1080p", "hash": "bbb", "size_bytes": 2100000000}], "year": 2021}
		], "count": 2}}
		""";

	private const string Xml = """
		<?xml version="1.0" encoding="UTF-8"?>
		<rss xmlns:torznab="http://torznab.com/schemas/2015/feed" version="1.0">
			<channel>
				<item>
					<title>Xml Alpha</title>
					<link>https://site.example/d/1</link>
					<input name="infohash" value="aaa111"/>
					<input name="size" value="1024"/>
				</item>
				<item>
					<title>Xml Beta</title>
					<link>https://site.example/d/2</link>
					<input name="infohash" value="bbb222"/>
					<input name="size" value="2048"/>
				</item>
			</channel>
		</rss>
		""";

	private static string RenderTemplate(string template, IReadOnlyDictionary<string, object?> result)
		=> new CardigannTemplateEngine().Render(
			template,
			new CardigannTemplateContext(
				new Dictionary<string, string> { ["sitelink"] = "https://site.example/" },
				new Dictionary<string, object?>(),
				"",
				[]),
			result);

	[Fact]
	public void SelectRows_ShouldMatchAllRows_WhenSingleSelector()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var rows = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row" });
		rows.Count.ShouldBe(3);
	}

	[Fact]
	public void SelectRows_ShouldApplyContainsPredicate_WhenSelectorContains()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var rows = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row:has(td.cat)" });
		rows.Count.ShouldBe(3);

		var filtered = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row:contains('Beta')" });
		filtered.ShouldHaveSingleItem();
	}

	[Fact]
	public void SelectRows_ShouldIntersectAlternatives_WhenAndMatchUsed()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var rows = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows
		{
			Selector = "tr.row, tr.hidden",
			Filters = [new CardigannFilter { Name = "andmatch" }]
		});
		rows.ShouldHaveSingleItem();
	}

	[Fact]
	public void SelectRows_ShouldSkipLeadingRows_WhenAfterSet()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var rows = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row", After = 2 });
		rows.ShouldHaveSingleItem();
	}

	[Fact]
	public void EvaluateField_ShouldReadTextAttributeAndRemove_WhenHtmlRow()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var row = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row" })[0];

		var title = engine.EvaluateField(row, CardigannResponseType.Html, new CardigannField { Selector = "td.name a", Attribute = "href" }, RenderTemplate, DateTime.UtcNow);
		title.ShouldBe("/d/1");

		var extra = engine.EvaluateField(row, CardigannResponseType.Html, new CardigannField { Selector = "td.extra", Remove = "span" }, RenderTemplate, DateTime.UtcNow);
		extra.ShouldBe("meta");
	}

	[Fact]
	public void EvaluateField_ShouldUseScopeSelectors_WhenSelectorStartsWithScope()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var row = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row" })[1];

		var title = engine.EvaluateField(row, CardigannResponseType.Html, new CardigannField { Selector = ":scope > td.name > a" }, RenderTemplate, DateTime.UtcNow);
		title.ShouldBe("Beta");
	}

	[Fact]
	public void EvaluateField_ShouldFallBackToDefault_WhenSelectorMisses()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var row = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row" })[2];

		var value = engine.EvaluateField(
			row,
			CardigannResponseType.Html,
			new CardigannField { Selector = "td.missing", Default = "fallback", Optional = true },
			RenderTemplate,
			DateTime.UtcNow);
		value.ShouldBe("fallback");
	}

	[Fact]
	public void EvaluateField_ShouldMapValuesViaCase_WhenCaseProvided()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var row = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row" })[0];

		var value = engine.EvaluateField(
			row,
			CardigannResponseType.Html,
			new CardigannField
			{
				Selector = "td.cat",
				Case = new Dictionary<string, string> { ["Movies"] = "1", ["*"] = "40" }
			},
			RenderTemplate,
			DateTime.UtcNow);
		value.ShouldBe("1");
	}

	[Fact]
	public void EvaluateField_ShouldPickFirstMatchingAlternative_WhenCommaSeparated()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Html, Html);
		var row = engine.SelectRows(document, CardigannResponseType.Html, new CardigannRows { Selector = "tr.row" })[0];

		var value = engine.EvaluateField(
			row,
			CardigannResponseType.Html,
			new CardigannField { Selector = "td.missing, td.name a", Attribute = "href" },
			RenderTemplate,
			DateTime.UtcNow);
		value.ShouldBe("/d/1");
	}

	[Fact]
	public void SelectRows_ShouldResolveJsonPathsAndMultiple_WhenJsonResponse()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Json, Json);
		var rows = engine.SelectRows(document, CardigannResponseType.Json, new CardigannRows
		{
			Selector = "data.movies",
			Attribute = "torrents",
			Multiple = true,
			MissingAttributeEqualsNoResults = true
		});

		rows.Count.ShouldBe(2);

		var quality = engine.EvaluateField(rows[0], CardigannResponseType.Json, new CardigannField { Selector = "quality" }, RenderTemplate, DateTime.UtcNow);
		quality.ShouldBe("720p");

		var title = engine.EvaluateField(rows[0], CardigannResponseType.Json, new CardigannField { Selector = "..title" }, RenderTemplate, DateTime.UtcNow);
		title.ShouldBe("Alpha");

		var year = engine.EvaluateField(rows[1], CardigannResponseType.Json, new CardigannField { Selector = "..year" }, RenderTemplate, DateTime.UtcNow);
		year.ShouldBe("2021");
	}

	[Fact]
	public void SelectRows_ShouldFilterRootRows_WhenHasContainsPredicateUsed()
	{
		const string json = """
			[{"id": "1", "name": "Alpha", "username": "alice"}, {"id": "2", "name": "Beta", "username": "bob"}]
			""";
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Json, json);
		var selector = new CardigannTemplateEngine().Render(
			"${{ if .Config.uploader }}:has(username:contains(alice)){{ else }}{{ end }}",
			new CardigannTemplateContext(
				new Dictionary<string, string> { ["uploader"] = "alice" },
				new Dictionary<string, object?>(),
				"",
				[]));
		var rows = engine.SelectRows(document, CardigannResponseType.Json, new CardigannRows { Selector = selector });

		rows.ShouldHaveSingleItem();
		engine.EvaluateField(rows[0], CardigannResponseType.Json, new CardigannField { Selector = "id" }, RenderTemplate, DateTime.UtcNow)
			.ShouldBe("1");
	}

	[Fact]
	public void SelectRows_ShouldSelectXmlItemsByLocalName_WhenXmlResponse()
	{
		var engine = new CardigannSelectorEngine();
		var document = engine.ParseResponse(CardigannResponseType.Xml, Xml);
		var rows = engine.SelectRows(document, CardigannResponseType.Xml, new CardigannRows { Selector = "rss > channel > item" });

		rows.Count.ShouldBe(2);

		var title = engine.EvaluateField(rows[0], CardigannResponseType.Xml, new CardigannField { Selector = "title" }, RenderTemplate, DateTime.UtcNow);
		title.ShouldBe("Xml Alpha");

		var hash = engine.EvaluateField(rows[1], CardigannResponseType.Xml, new CardigannField { Selector = "[name=infohash]", Attribute = "value" }, RenderTemplate, DateTime.UtcNow);
		hash.ShouldBe("bbb222");
	}
}
