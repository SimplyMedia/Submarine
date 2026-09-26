using FluentValidation;
using Shouldly;
using Submarine.Api.Common;
using Submarine.Core.Entities;
using Xunit;

namespace Submarine.Api.Tests.Common;

public sealed class PagedResultTests
{
	private static IQueryable<User> Users() => new List<User>
	{
		new() { Id = 1, Username = "alpha" },
		new() { Id = 2, Username = "bravo" },
		new() { Id = 3, Username = "charlie" },
		new() { Id = 4, Username = "delta" },
		new() { Id = 5, Username = "echo" }
	}.AsQueryable();

	[Fact]
	public async Task CreateAsync_ShouldPageItems_AndReportTotalCount()
	{
		var result = await PagedResult<User>.CreateAsync(Users(), new PagingQuery(Page: 2, PageSize: 2), TestContext.Current.CancellationToken);

		result.Page.ShouldBe(2);
		result.PageSize.ShouldBe(2);
		result.TotalCount.ShouldBe(5);
		result.Items.Select(x => x.Username).ShouldBe(["charlie", "delta"]);
	}

	[Fact]
	public async Task CreateAsync_ShouldSortDescending_ByProperty()
	{
		var result = await PagedResult<User>.CreateAsync(
			Users(),
			new PagingQuery(SortKey: "username", SortDirection: "descending"),
			TestContext.Current.CancellationToken);

		result.Items.Select(x => x.Username).ShouldBe(["echo", "delta", "charlie", "bravo", "alpha"]);
	}

	[Fact]
	public async Task CreateAsync_ShouldSortAscending_ByDefault()
	{
		var result = await PagedResult<User>.CreateAsync(Users(), new PagingQuery(SortKey: "Username"), TestContext.Current.CancellationToken);

		result.Items.First().Username.ShouldBe("alpha");
	}

	[Fact]
	public async Task CreateAsync_ShouldClampPageAndPageSize()
	{
		var result = await PagedResult<User>.CreateAsync(Users(), new PagingQuery(Page: -3, PageSize: 0), TestContext.Current.CancellationToken);

		result.Page.ShouldBe(1);
		result.PageSize.ShouldBe(1);
		result.Items.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task CreateAsync_ShouldLeaveSourceOrderUntouched_WhenNoSortKeyGiven()
	{
		// The simple overload may receive an already-projected positional record, which EF cannot
		// re-order after the fact, so it must not invent a default order.
		var shuffled = new List<User>
		{
			new() { Id = 3, Username = "charlie" },
			new() { Id = 1, Username = "alpha" },
			new() { Id = 2, Username = "bravo" }
		}.AsQueryable();

		var result = await PagedResult<User>.CreateAsync(shuffled, new PagingQuery(), TestContext.Current.CancellationToken);

		result.Items.Select(x => x.Id).ShouldBe([3, 1, 2]);
	}

	[Fact]
	public async Task CreateAsync_WithProjector_ShouldDefaultSortById_WhenNoSortKeyGiven()
	{
		var shuffled = new List<User>
		{
			new() { Id = 3, Username = "charlie" },
			new() { Id = 1, Username = "alpha" },
			new() { Id = 2, Username = "bravo" }
		}.AsQueryable();

		var result = await PagedResult<string>.CreateAsync(shuffled, new PagingQuery(), x => x.Username, TestContext.Current.CancellationToken);

		result.Items.ShouldBe(["alpha", "bravo", "charlie"]);
	}

	[Fact]
	public async Task CreateAsync_WithProjector_ShouldSortBeforeProjecting_WhenSortKeyGiven()
	{
		var result = await PagedResult<string>.CreateAsync(
			Users(),
			new PagingQuery(SortKey: "Username", SortDirection: "descending"),
			x => x.Username,
			TestContext.Current.CancellationToken);

		result.Items.ShouldBe(["echo", "delta", "charlie", "bravo", "alpha"]);
	}

	[Fact]
	public async Task CreateAsync_ShouldThrowValidation_WhenSortKeyIsUnknown()
		=> await Should.ThrowAsync<ValidationException>(async () =>
			await PagedResult<User>.CreateAsync(Users(), new PagingQuery(SortKey: "nope"), TestContext.Current.CancellationToken));
}
