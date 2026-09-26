using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class CardigannIndexerLoginTests
{
	private const string LoginPage = """
		<html><body>
		<form id="login" action="/takelogin" method="post">
			<input type="hidden" name="token" value="abc123"/>
			<input type="text" name="username"/>
			<input type="password" name="password"/>
		</form>
		</body></html>
		""";

	private const string LoggedInPage = """
		<html><body><a href="/logout.php">logout</a></body></html>
		""";

	private const string LoginFailedPage = """
		<html><body><table><tr><td class="komunikaty">Bad credentials, try again</td></tr></table></body></html>
		""";

	private const string Definition = """
		---
		id: testlogin
		name: TestLogin
		language: en-US
		type: semi-private
		encoding: UTF-8
		links:
		  - https://private.example/
		caps:
		  categorymappings:
		    - {id: 1, cat: TV, desc: tv}
		  modes:
		    search: [q]
		settings:
		  - name: username
		    type: text
		  - name: password
		    type: password
		login:
		  path: /login
		  method: form
		  form: form#login
		  inputs:
		    username: "{{ .Config.username }}"
		    password: "{{ .Config.password }}"
		  error:
		    - selector: td.komunikaty:contains("Bad credentials")
		      message: Wrong username or password
		  test:
		    path: /
		    selector: a[href$="/logout.php"]
		search:
		  paths:
		    - path: browse
		  rows:
		    selector: tr
		  fields:
		    title:
		      selector: td.title
		    details:
		      selector: td.title a
		      attribute: href
		""";

	private const string ResultsPage = """
		<html><body><table><tr><td class="title"><a href="/details/9">Release One</a></td></tr></table></body></html>
		""";

	[Fact]
	public async Task FetchAsync_ShouldSubmitFormWithMergedFields_WhenLoginRequired()
	{
		var http = new FakeIndexerHttpClient(request =>
		{
			var path = request.PathAndQuery;
			return path switch
			{
				"/login" => FakeResponses.Html(LoginPage),
				"/takelogin" => FakeResponses.Html(LoggedInPage),
				"/" or "/browse" => path == "/" ? FakeResponses.Html(LoggedInPage) : FakeResponses.Html(ResultsPage),
				_ => FakeResponses.Html(ResultsPage)
			};
		});
		await using var indexer = CreateIndexer(http, "user", "right");

		var releases = await indexer.Fetch(new BasicSearchRequest());

		http.Requests.Count.ShouldBe(4);
		http.Requests[0].PathAndQuery.ShouldBe("/login");
		var loginPost = http.Requests[1];
		loginPost.Method.ShouldBe(System.Net.Http.HttpMethod.Post);
		loginPost.PathAndQuery.ShouldBe("/takelogin");
		loginPost.Fields["username"].ShouldBe("user");
		loginPost.Fields["password"].ShouldBe("right");
		loginPost.Fields["token"].ShouldBe("abc123");
		http.Requests[2].PathAndQuery.ShouldBe("/");
		http.Requests[3].PathAndQuery.ShouldBe("/browse");

		releases.ShouldHaveSingleItem().Title.ShouldBe("Release One");
	}

	[Fact]
	public async Task FetchAsync_ShouldThrowAuthException_WhenErrorSelectorMatches()
	{
		var http = new FakeIndexerHttpClient(request => request.PathAndQuery switch
		{
			"/login" => FakeResponses.Html(LoginPage),
			"/takelogin" => FakeResponses.Html(LoginFailedPage),
			_ => FakeResponses.Html(ResultsPage)
		});
		await using var indexer = CreateIndexer(http, "user", "wrong");

		(await Should.ThrowAsync<IndexerAuthException>(async () => await indexer.Fetch(new BasicSearchRequest())))
			.Message.ShouldBe("Wrong username or password");
	}

	[Fact]
	public async Task FetchAsync_ShouldThrowAuthException_WhenLoginTestSelectorMissing()
	{
		var http = new FakeIndexerHttpClient(request => request.PathAndQuery switch
		{
			"/login" => FakeResponses.Html(LoginPage),
			"/takelogin" => FakeResponses.Html("<html><body>no logout here</body></html>"),
			_ => FakeResponses.Html(ResultsPage)
		});
		await using var indexer = CreateIndexer(http, "user", "right");

		(await Should.ThrowAsync<IndexerAuthException>(async () => await indexer.Fetch(new BasicSearchRequest())))
			.Message.ShouldContain("login test");
	}

	[Fact]
	public async Task FetchAsync_ShouldLoginOnlyOnce_WhenMultipleSearches()
	{
		var http = new FakeIndexerHttpClient(request =>
		{
			var path = request.PathAndQuery;
			return path switch
			{
				"/login" => FakeResponses.Html(LoginPage),
				"/takelogin" => FakeResponses.Html(LoggedInPage),
				"/" or "/browse" => path == "/" ? FakeResponses.Html(LoggedInPage) : FakeResponses.Html(ResultsPage),
				_ => FakeResponses.Html(ResultsPage)
			};
		});
		await using var indexer = CreateIndexer(http, "user", "right");

		await indexer.Fetch(new BasicSearchRequest());
		await indexer.Fetch(new BasicSearchRequest());

		http.Requests.Count(request => request.PathAndQuery == "/takelogin").ShouldBe(1);
		http.Requests.Count.ShouldBe(5);
	}

	[Fact]
	public async Task FetchAsync_ShouldSetCookieHeader_WhenCookieLoginUsed()
	{
		const string cookieDefinition = """
			---
			id: testcookie
			name: TestCookie
			language: en-US
			type: private
			encoding: UTF-8
			links:
			  - https://cookie.example/
			caps:
			  modes:
			    search: [q]
			settings:
			  - name: cookie
			    type: text
			login:
			  path: /
			  method: cookie
			  cookie: cookie
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(ResultsPage));
		var definition = CardigannDefinitionParser.Parse(cookieDefinition);
		var settings = new CardigannSettings("testcookie", Fields: new Dictionary<string, string> { ["cookie"] = "uid=42; pass=secret" });
		await using var indexer = new CardigannIndexer(definition, settings, http, new NullLogger<CardigannIndexer>());

		await indexer.Fetch(new BasicSearchRequest());

		var cookieHeader = http.Cookies.GetCookieHeader(new Uri("https://cookie.example/"));
		cookieHeader.ShouldContain("uid=42");
		cookieHeader.ShouldContain("pass=secret");
	}

	[Fact]
	public async Task FetchAsync_ShouldThrowAuthException_WhenCookieFieldMissing()
	{
		const string cookieDefinition = """
			---
			id: testcookie2
			name: TestCookie2
			language: en-US
			type: private
			encoding: UTF-8
			links:
			  - https://cookie.example/
			caps:
			  modes:
			    search: [q]
			settings:
			  - name: cookie
			    type: text
			login:
			  path: /
			  method: cookie
			  cookie: cookie
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(ResultsPage));
		var definition = CardigannDefinitionParser.Parse(cookieDefinition);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testcookie2"), http, new NullLogger<CardigannIndexer>());

		(await Should.ThrowAsync<IndexerAuthException>(async () => await indexer.Fetch(new BasicSearchRequest())))
			.Message.ShouldContain("cookie");
	}

	[Fact]
	public async Task FetchAsync_ShouldGetWithInputs_WhenGetLoginUsed()
	{
		const string getDefinition = """
			---
			id: testgetlogin
			name: TestGetLogin
			language: en-US
			type: private
			encoding: UTF-8
			links:
			  - https://api.example/
			caps:
			  modes:
			    search: [q]
			settings:
			  - name: apikey
			    type: text
			login:
			  path: "https://api.example/api/torznab"
			  method: get
			  inputs:
			    apikey: "{{ .Config.apikey }}"
			    t: search
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(ResultsPage));
		var definition = CardigannDefinitionParser.Parse(getDefinition);
		var settings = new CardigannSettings("testgetlogin", Fields: new Dictionary<string, string> { ["apikey"] = "key1" });
		await using var indexer = new CardigannIndexer(definition, settings, http, new NullLogger<CardigannIndexer>());

		await indexer.Fetch(new BasicSearchRequest());

		http.Requests[0].Uri.ToString().ShouldBe("https://api.example/api/torznab?apikey=key1&t=search");
		http.Requests[0].Method.ShouldBe(System.Net.Http.HttpMethod.Get);
	}

	[Fact]
	public async Task FetchAsync_ShouldPostInputs_WhenPostLoginUsed()
	{
		const string postDefinition = """
			---
			id: testpostlogin
			name: TestPostLogin
			language: en-US
			type: private
			encoding: UTF-8
			links:
			  - https://post.example/
			caps:
			  modes:
			    search: [q]
			settings:
			  - name: username
			    type: text
			  - name: password
			    type: password
			login:
			  path: takelogin.php
			  method: post
			  inputs:
			    username: "{{ .Config.username }}"
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(ResultsPage));
		var definition = CardigannDefinitionParser.Parse(postDefinition);
		var settings = new CardigannSettings("testpostlogin", Fields: new Dictionary<string, string> { ["username"] = "u", ["password"] = "p" });
		await using var indexer = new CardigannIndexer(definition, settings, http, new NullLogger<CardigannIndexer>());

		await indexer.Fetch(new BasicSearchRequest());

		var login = http.Requests[0];
		login.Method.ShouldBe(System.Net.Http.HttpMethod.Post);
		login.Uri.ToString().ShouldBe("https://post.example/takelogin.php");
		login.Fields["username"].ShouldBe("u");
	}

	[Fact]
	public async Task FetchAsync_ShouldThrowAuthException_WhenLoginMethodUnsupported()
	{
		const string captchaDefinition = """
			---
			id: testcaptcha
			name: TestCaptcha
			language: en-US
			type: private
			encoding: UTF-8
			links:
			  - https://captcha.example/
			caps:
			  modes:
			    search: [q]
			settings: []
			login:
			  path: /
			  method: captcha
			  captcha:
			    type: image
			search:
			  paths:
			    - path: browse
			  rows:
			    selector: tr
			  fields:
			    title:
			      selector: td.title
			""";

		var http = new FakeIndexerHttpClient(_ => FakeResponses.Html(ResultsPage));
		var definition = CardigannDefinitionParser.Parse(captchaDefinition);
		await using var indexer = new CardigannIndexer(definition, new CardigannSettings("testcaptcha"), http, new NullLogger<CardigannIndexer>());

		(await Should.ThrowAsync<IndexerAuthException>(async () => await indexer.Fetch(new BasicSearchRequest())))
			.Message.ShouldContain("captcha");
	}

	private static CardigannIndexer CreateIndexer(FakeIndexerHttpClient http, string username, string password)
	{
		var definition = CardigannDefinitionParser.Parse(Definition);
		var settings = new CardigannSettings("testlogin", Fields: new Dictionary<string, string>
		{
			["username"] = username,
			["password"] = password
		});
		return new CardigannIndexer(definition, settings, http, new NullLogger<CardigannIndexer>());
	}
}
