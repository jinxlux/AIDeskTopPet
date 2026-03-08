using DesktopPet.Core;

namespace DesktopPet.Tests.Core;

public sealed class PetBehaviorEngineTests
{
    [Fact]
    public void TryInteract_ShouldEnterInteract_WhenNotInCooldown()
    {
        var engine = new PetBehaviorEngine(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
        var now = DateTimeOffset.Parse("2026-03-01T12:00:00+08:00");

        var accepted = engine.TryInteract(now);

        Assert.True(accepted);
        Assert.Equal(PetState.Interact, engine.CurrentState);
    }

    [Fact]
    public void Tick_ShouldReturnIdle_AfterDurationElapsed()
    {
        var engine = new PetBehaviorEngine(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
        var now = DateTimeOffset.Parse("2026-03-01T12:00:00+08:00");
        engine.TryInteract(now);

        var changed = engine.Tick(now.AddSeconds(1.1));

        Assert.True(changed);
        Assert.Equal(PetState.Idle, engine.CurrentState);
    }

    [Fact]
    public void TryInteract_ShouldReject_WhenCooldownNotElapsed()
    {
        var engine = new PetBehaviorEngine(TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(2));
        var now = DateTimeOffset.Parse("2026-03-01T12:00:00+08:00");
        engine.TryInteract(now);

        var accepted = engine.TryInteract(now.AddMilliseconds(700));

        Assert.False(accepted);
    }

    [Fact]
    public void ForceIdle_ShouldSetStateToIdle()
    {
        var engine = new PetBehaviorEngine(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
        var now = DateTimeOffset.Parse("2026-03-01T12:00:00+08:00");
        engine.TryInteract(now);

        engine.ForceIdle();

        Assert.Equal(PetState.Idle, engine.CurrentState);
    }
}
