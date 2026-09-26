using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Core.Events;
using Xunit;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Proves the event bus reaches both typed handlers and the SignalR hub for a real command run.
/// </summary>
public sealed class EventDispatchTests
{
	[Fact]
	public async Task EnqueuedCommand_ShouldReachTypedHandlerAndHubClients()
	{
		var recorder = new RecordingHandler();
		await using var factory = new RecordingApiFactory(recorder);
		var client = await factory.CreateAuthorizedClientAsync();
		var apiKey = client.DefaultRequestHeaders.GetValues("X-Api-Key").Single();

		var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var hub = new HubConnectionBuilder()
			.WithUrl($"{client.BaseAddress}hubs/events?apikey={apiKey}", options =>
				options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler())
			.Build();
		hub.On<JsonElement>("event", envelope =>
		{
			if (envelope.GetProperty("type").GetString() == nameof(CommandUpdated)
				&& envelope.GetProperty("payload").GetProperty("status").GetString() == "COMPLETED")
			{
				received.TrySetResult(envelope);
			}
		});
		await hub.StartAsync(TestContext.Current.CancellationToken);

		var response = await client.PostAsJsonAsync("/api/v1/commands", new { name = "CommandCleanup" }, TestContext.Current.CancellationToken);
		response.EnsureSuccessStatusCode();

		var envelope = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
		envelope.GetProperty("payload").GetProperty("name").GetString().ShouldBe("CommandCleanup");
		await recorder.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
	}

	private sealed class RecordingApiFactory(RecordingHandler recorder) : SubmarineApiFactory
	{
		protected override void ConfigureTestServices(IServiceCollection services)
			=> services.AddSingleton<IEventHandler<CommandUpdated>>(recorder);
	}

	private sealed class RecordingHandler : IEventHandler<CommandUpdated>
	{
		public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public Task HandleAsync(CommandUpdated @event, CancellationToken cancellationToken = default)
		{
			if (@event.Status == Core.Commands.CommandStatus.COMPLETED)
			{
				Completed.TrySetResult();
			}

			return Task.CompletedTask;
		}
	}
}
