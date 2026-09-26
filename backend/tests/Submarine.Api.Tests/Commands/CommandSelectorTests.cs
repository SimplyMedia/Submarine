using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Commands;
using Xunit;

namespace Submarine.Api.Tests.Commands;

public sealed class CommandSelectorTests
{
	[Fact]
	public void SelectNext_ShouldPreferHighPriority_ThenOldest()
	{
		var low = new Command { Id = 3, Name = "Low", Priority = CommandPriority.LOW };
		var highOld = new Command { Id = 2, Name = "HighOld", Priority = CommandPriority.HIGH };
		var highNew = new Command { Id = 5, Name = "HighNew", Priority = CommandPriority.HIGH };
		var normal = new Command { Id = 1, Name = "Normal", Priority = CommandPriority.NORMAL };

		var selected = CommandSelector.SelectNext([low, highNew, normal, highOld], new HashSet<string>());

		selected.ShouldBe(highOld);
	}

	[Fact]
	public void SelectNext_ShouldSkipRunningNames()
	{
		var busy = new Command { Id = 1, Name = "Busy", Priority = CommandPriority.HIGH };
		var other = new Command { Id = 2, Name = "Other", Priority = CommandPriority.NORMAL };

		var selected = CommandSelector.SelectNext([busy, other], new HashSet<string>(["Busy"]));

		selected.ShouldBe(other);
	}

	[Fact]
	public void SelectNext_ShouldReturnNull_WhenEverythingIsBlocked()
	{
		var busy = new Command { Id = 1, Name = "Busy" };

		CommandSelector.SelectNext([busy], new HashSet<string>(["Busy"])).ShouldBeNull();
	}
}
