using System.Collections.Generic;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class CardigannTemplateEngineTests
{
	private static readonly IReadOnlyDictionary<string, string> Config = new Dictionary<string, string>
	{
		["sitelink"] = "https://tracker.example/",
		["disablesort"] = "false",
		["sort"] = "time",
		["empty"] = ""
	};

	private static readonly IReadOnlyDictionary<string, object?> Query = CardigannTemplateContext.BuildQuery(
		"tvsearch",
		"the boys",
		"the boys",
		season: 2,
		episode: 5,
		tvdbId: 280619,
		tmdbId: 1234,
		imdbId: "tt1190634",
		year: 2019,
		limit: 100,
		offset: 50);

	private static readonly IReadOnlyList<string> Categories = ["1_0", "2_1"];

	private static CardigannTemplateContext Context(
		IReadOnlyDictionary<string, object?>? query = null,
		string keywords = "the boys",
		IReadOnlyList<string>? categories = null)
		=> new(Config, query ?? Query, keywords, categories ?? Categories);

	[Fact]
	public void Render_ShouldPassThroughText_WhenNoActions()
		=> new CardigannTemplateEngine().Render("search.php?q=test", Context())
			.ShouldBe("search.php?q=test");

	[Theory]
	[InlineData("{{ .Keywords }}", "the boys")]
	[InlineData("{{ .Query.Q }}", "the boys")]
	[InlineData("{{ .Query.Type }}", "tvsearch")]
	[InlineData("{{ .Query.Season }}", "2")]
	[InlineData("{{ .Query.Ep }}", "5")]
	[InlineData("{{ .Query.Limit }}", "100")]
	[InlineData("{{ .Query.Offset }}", "50")]
	[InlineData("{{ .Query.TVDBID }}", "280619")]
	[InlineData("{{ .Query.TMDBID }}", "1234")]
	[InlineData("{{ .Query.IMDBID }}", "tt1190634")]
	[InlineData("{{ .Query.IMDBIDShort }}", "1190634")]
	[InlineData("{{ .Query.Year }}", "2019")]
	[InlineData("{{ .Config.sitelink }}", "https://tracker.example/")]
	[InlineData("{{ .Config.sort }}", "time")]
	[InlineData("{{ .True }}/{{ .False }}", "true/false")]
	public void Render_ShouldResolveValues_WhenPathsKnown(string template, string expected)
		=> new CardigannTemplateEngine().Render(template, Context()).ShouldBe(expected);

	[Fact]
	public void Render_ShouldResolveTodayYear_WhenRequested()
		=> new CardigannTemplateEngine().Render("{{ .Today.Year }}", Context())
			.ShouldBe(DateTime.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));

	[Fact]
	public void Render_ShouldRenderEmpty_WhenPathUnknown()
		=> new CardigannTemplateEngine().Render("x{{ .Config.nope }}y", Context()).ShouldBe("xy");

	[Fact]
	public void Render_ShouldJoinCategories_WhenJoinCalled()
		=> new CardigannTemplateEngine().Render("{{ join .Categories \",\" }}", Context())
			.ShouldBe("1_0,2_1");

	[Fact]
	public void Render_ShouldReplaceViaRegex_WhenReReplaceCalled()
		=> new CardigannTemplateEngine().Render("{{ re_replace .Query.Keywords \"[^a-zA-Z0-9]+\" \"%\" }}", Context())
			.ShouldBe("the%boys");

	[Fact]
	public void Render_ShouldEvaluateIfElse_WhenConditionTruthy()
		=> new CardigannTemplateEngine().Render("{{ if .Keywords }}has{{ else }}none{{ end }}", Context())
			.ShouldBe("has");

	[Fact]
	public void Render_ShouldEvaluateElse_WhenConditionFalsy()
	{
		var context = Context(keywords: "");
		new CardigannTemplateEngine().Render("{{ if .Keywords }}has{{ else }}none{{ end }}", context)
			.ShouldBe("none");
	}

	[Fact]
	public void Render_ShouldSkipIfWithoutElse_WhenConditionFalsy()
		=> new CardigannTemplateEngine().Render("a{{ if .Config.empty }}x{{ end }}b", Context())
			.ShouldBe("ab");

	[Fact]
	public void Render_ShouldCompareStrings_WhenEqUsed()
	{
		var engine = new CardigannTemplateEngine();
		engine.Render("{{ if eq .Config.disablesort \"false\" }}no-sort{{ else }}sort{{ end }}", Context())
			.ShouldBe("no-sort");
		engine.Render("{{ if eq .Config.disablesort .False }}no-sort{{ else }}sort{{ end }}", Context())
			.ShouldBe("no-sort");
	}

	[Fact]
	public void Render_ShouldSupportAndOrNot_WhenCombined()
	{
		var engine = new CardigannTemplateEngine();
		engine.Render("{{ if and (.Keywords) (eq .Config.disablesort .False) }}A{{ else }}B{{ end }}", Context())
			.ShouldBe("A");
		engine.Render("{{ if and (.Keywords) (.Config.empty) }}A{{ else }}B{{ end }}", Context())
			.ShouldBe("B");
		engine.Render("{{ if or (.Config.empty) (.Keywords) }}A{{ else }}B{{ end }}", Context())
			.ShouldBe("A");
		engine.Render("{{ if not (.Config.empty) }}A{{ else }}B{{ end }}", Context())
			.ShouldBe("A");
	}

	[Fact]
	public void Render_ShouldReturnFirstTruthyValue_WhenOrUsedWithResults()
	{
		var result = new Dictionary<string, object?> { ["a"] = "", ["b"] = "second" };
		new CardigannTemplateEngine().Render("{{ or .Result.a .Result.b }}", Context(), result)
			.ShouldBe("second");
	}

	[Fact]
	public void Render_ShouldIterateCategories_WhenRangeUsed()
		=> new CardigannTemplateEngine().Render("{{ range .Categories }}[{{ . }}]{{ end }}", Context())
			.ShouldBe("[1_0][2_1]");

	[Fact]
	public void Render_ShouldNestTemplates_WhenUsedAsFunctionArguments()
		=> new CardigannTemplateEngine().Render("{{ re_replace .Keywords \"the\" (join .Categories \"\") }}", Context())
			.ShouldBe("1_02_1 boys");

	[Fact]
	public void Render_ShouldSupportNeOnNumbers_WhenCompared()
		=> new CardigannTemplateEngine().Render("{{ if ne .Query.Season 3 }}s2{{ end }}", Context())
			.ShouldBe("s2");

	[Fact]
	public void RenderCondition_ShouldEvaluatePipelineTruthiness()
	{
		var engine = new CardigannTemplateEngine();
		engine.RenderCondition(".Keywords", Context(), new Dictionary<string, object?>()).ShouldBeTrue();
		engine.RenderCondition("eq .Query.Type \"tvsearch\"", Context(), new Dictionary<string, object?>()).ShouldBeTrue();
		engine.RenderCondition(".Config.empty", Context(), new Dictionary<string, object?>()).ShouldBeFalse();
	}

	[Fact]
	public void RenderAll_ShouldOmitEmptyEntries_WhenTemplatesRenderEmpty()
	{
		var rendered = new CardigannTemplateEngine().RenderAll(
			new Dictionary<string, string>
			{
				["q"] = "{{ .Keywords }}",
				["season"] = "{{ .Query.Season }}",
				["missing"] = "{{ .Config.nope }}"
			},
			Context());

		rendered.Keys.ShouldBe(["q", "season"]);
		rendered["season"].ShouldBe("2");
	}

	[Fact]
	public void Render_ShouldThrow_WhenActionNeverCloses()
		=> Should.Throw<CardigannTemplateException>(() => new CardigannTemplateEngine().Render("{{ .Keywords ", Context()));
}
