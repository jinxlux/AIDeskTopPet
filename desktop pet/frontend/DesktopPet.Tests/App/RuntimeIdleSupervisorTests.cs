using System;
using System.Threading;
using System.Threading.Tasks;
using DesktopPet.App.Services;

namespace DesktopPet.Tests.App;

public class RuntimeIdleSupervisorTests
{
    [Fact]
    public async Task TriggersStop_WhenIdleTimeoutReached()
    {
        var callCount = 0;
        using var supervisor = new RuntimeIdleSupervisor(
            idleTimeout: TimeSpan.FromMilliseconds(80),
            checkInterval: TimeSpan.FromMilliseconds(30),
            stopAction: _ =>
            {
                Interlocked.Increment(ref callCount);
                return Task.CompletedTask;
            });

        supervisor.Start();
        supervisor.MarkInteraction();
        await Task.Delay(250);

        supervisor.Stop();
        Assert.True(callCount >= 1);
    }

    [Fact]
    public async Task DoesNotTriggerStop_WhenRuntimeMarkedStopped()
    {
        var callCount = 0;
        using var supervisor = new RuntimeIdleSupervisor(
            idleTimeout: TimeSpan.FromMilliseconds(80),
            checkInterval: TimeSpan.FromMilliseconds(30),
            stopAction: _ =>
            {
                Interlocked.Increment(ref callCount);
                return Task.CompletedTask;
            });

        supervisor.Start();
        supervisor.MarkInteraction();
        supervisor.MarkRuntimeStopped();
        await Task.Delay(250);

        supervisor.Stop();
        Assert.Equal(0, callCount);
    }
}
