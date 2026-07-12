using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Exceptions;
using Submarine.Api.Jobs;

namespace Submarine.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class SystemTaskController : ControllerBase
{
	private readonly IScheduledJobRegistry _registry;
	private readonly ScheduledJobRunner _runner;
	private readonly IBackgroundTaskQueue _taskQueue;

	public SystemTaskController(IScheduledJobRegistry registry, ScheduledJobRunner runner,
		IBackgroundTaskQueue taskQueue)
	{
		_registry = registry;
		_runner = runner;
		_taskQueue = taskQueue;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyCollection<ScheduledJobStatus>), StatusCodes.Status200OK)]
	public IActionResult GetAll()
		=> Ok(_registry.GetAll());

	[HttpPost("{name}")]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> TriggerAsync([FromRoute] string name)
	{
		if (!_runner.Contains(name))
			throw new NotFoundException();

		if (_runner.TryStart(name))
			await _taskQueue.QueueAsync((sp, ct) => _runner.RunAsync(name, sp, ct));

		return Accepted();
	}
}
