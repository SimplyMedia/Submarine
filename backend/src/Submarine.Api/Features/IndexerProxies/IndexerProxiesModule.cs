using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Common;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.IndexerProxies;

/// <summary>
///     Indexer proxy CRUD and connection testing.
/// </summary>
public sealed class IndexerProxiesModule : IEndpointModule
{
	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/api/v1/indexer-proxies");
		group.MapGet("/", ListAsync);
		group.MapGet("/{id:int}", GetAsync);
		group.MapPost("/", CreateAsync);
		group.MapPost("/{id:int}/test", TestAsync);
		group.MapPut("/{id:int}", UpdateAsync);
		group.MapDelete("/{id:int}", DeleteAsync);
	}

	private static async Task<Ok<PagedResult<IndexerProxyDto>>> ListAsync(
		SubmarineDbContext db,
		[AsParameters] PagingQuery query,
		CancellationToken cancellationToken)
	{
		var proxies = await db.IndexerProxies.AsNoTracking().Include(proxy => proxy.Tags).OrderBy(proxy => proxy.Name).ToListAsync(cancellationToken);
		return TypedResults.Ok(await PagedResult<IndexerProxyDto>.CreateAsync(proxies.Select(ToDto).AsQueryable(), query, cancellationToken));
	}

	private static async Task<Results<Ok<IndexerProxyDto>, NotFound>> GetAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var proxy = await db.IndexerProxies.AsNoTracking().Include(entity => entity.Tags).FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		return proxy is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(proxy));
	}

	private static async Task<Created<IndexerProxyDto>> CreateAsync(
		SubmarineDbContext db,
		IValidator<IndexerProxyRequest> validator,
		[FromBody] IndexerProxyRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var proxy = new IndexerProxy();
		await ApplyAsync(proxy, request, db, cancellationToken);

		db.IndexerProxies.Add(proxy);
		await db.SaveChangesAsync(cancellationToken);

		return TypedResults.Created($"/api/v1/indexer-proxies/{proxy.Id}", ToDto(proxy));
	}

	private static async Task<Results<Ok<IndexerProxyDto>, NotFound>> UpdateAsync(
		int id,
		SubmarineDbContext db,
		IValidator<IndexerProxyRequest> validator,
		[FromBody] IndexerProxyRequest request,
		CancellationToken cancellationToken)
	{
		await validator.ValidateOrThrowAsync(request, cancellationToken);

		var proxy = await db.IndexerProxies.Include(entity => entity.Tags).FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (proxy is null)
		{
			return TypedResults.NotFound();
		}

		await ApplyAsync(proxy, request, db, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.Ok(ToDto(proxy));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteAsync(int id, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		var proxy = await db.IndexerProxies.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (proxy is null)
		{
			return TypedResults.NotFound();
		}

		db.IndexerProxies.Remove(proxy);
		await db.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
	}

	private static async Task<Results<Ok<IndexerProxyTestResult>, NotFound>> TestAsync(
		int id,
		SubmarineDbContext db,
		IHttpClientFactory httpClientFactory,
		CancellationToken cancellationToken)
	{
		var proxy = await db.IndexerProxies.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (proxy is null)
		{
			return TypedResults.NotFound();
		}

		return TypedResults.Ok(await RunTestAsync(proxy, httpClientFactory, cancellationToken));
	}

	private static async Task<IndexerProxyTestResult> RunTestAsync(IndexerProxy proxy, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	{
		try
		{
			if (proxy.Type == IndexerProxyType.FLARESOLVERR)
			{
				using var client = httpClientFactory.CreateClient();
				client.Timeout = TimeSpan.FromSeconds(proxy.RequestTimeoutSeconds);
				var response = await client.GetAsync($"http://{proxy.Host}:{proxy.Port}/", cancellationToken);
				var body = await response.Content.ReadAsStringAsync(cancellationToken);
				using var document = JsonDocument.Parse(body);
				return document.RootElement.TryGetProperty("msg", out _)
					? new IndexerProxyTestResult(true, null)
					: new IndexerProxyTestResult(false, "FlareSolverr did not report a status message");
			}

			using var handler = new HttpClientHandler
			{
				Proxy = new WebProxy(proxy.Host, proxy.Port)
				{
					Credentials = proxy.Username is null ? null : new NetworkCredential(proxy.Username, proxy.Password)
				},
				UseProxy = true
			};
			using var proxiedClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(proxy.RequestTimeoutSeconds) };
			var googleResponse = await proxiedClient.GetAsync("https://www.google.com", cancellationToken);
			return googleResponse.IsSuccessStatusCode
				? new IndexerProxyTestResult(true, null)
				: new IndexerProxyTestResult(false, $"Proxy returned {(int)googleResponse.StatusCode}");
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
		{
			return new IndexerProxyTestResult(false, exception.Message);
		}
	}

	private static async Task ApplyAsync(IndexerProxy proxy, IndexerProxyRequest request, SubmarineDbContext db, CancellationToken cancellationToken)
	{
		proxy.Name = request.Name;
		proxy.Type = request.Type;
		proxy.Host = request.Host;
		proxy.Port = request.Port;
		proxy.Username = request.Username;
		proxy.Password = request.Password;
		proxy.RequestTimeoutSeconds = request.RequestTimeoutSeconds;

		if (request.TagIds is not null)
		{
			var tags = await db.Tags.Where(tag => request.TagIds.Contains(tag.Id)).ToListAsync(cancellationToken);
			proxy.Tags.Clear();
			foreach (var tag in tags)
			{
				proxy.Tags.Add(tag);
			}
		}
	}

	private static IndexerProxyDto ToDto(IndexerProxy proxy)
		=> new(
			proxy.Id,
			proxy.Name,
			proxy.Type,
			proxy.Host,
			proxy.Port,
			proxy.Username,
			proxy.RequestTimeoutSeconds,
			[.. proxy.Tags.Select(tag => tag.Id)]);
}

/// <summary>A configured indexer proxy. The password is never returned.</summary>
public sealed record IndexerProxyDto(
	int Id,
	string Name,
	IndexerProxyType Type,
	string Host,
	int Port,
	string? Username,
	int RequestTimeoutSeconds,
	IReadOnlyList<int> TagIds);

/// <summary>Create or update request for an indexer proxy.</summary>
public sealed record IndexerProxyRequest(
	string Name,
	IndexerProxyType Type,
	string Host,
	int Port,
	string? Username,
	string? Password,
	int RequestTimeoutSeconds,
	List<int>? TagIds);

/// <summary>Result of a proxy connectivity test.</summary>
public sealed record IndexerProxyTestResult(bool IsValid, string? Message);

/// <summary>Validator for <see cref="IndexerProxyRequest" />.</summary>
public sealed class IndexerProxyRequestValidator : AbstractValidator<IndexerProxyRequest>
{
	/// <inheritdoc />
	public IndexerProxyRequestValidator()
	{
		RuleFor(request => request.Name).NotEmpty().MaximumLength(256);
		RuleFor(request => request.Type).IsInEnum();
		RuleFor(request => request.Host).NotEmpty();
		RuleFor(request => request.Port).InclusiveBetween(1, 65535);
		RuleFor(request => request.RequestTimeoutSeconds).GreaterThan(0);
	}
}
