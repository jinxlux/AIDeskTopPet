using System;
using System.Collections.Generic;
using DesktopPet.App.Services;
using DesktopPet.Core;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet.Tests.App;

public class PetAnimationOrchestratorTests
{
    [Fact]
    public void LoadSequences_SetsInitialIdleFrame()
    {
        var orchestrator = new PetAnimationOrchestrator(new Random(1));
        orchestrator.LoadSequences(
            [CreateSequence("idle-a", 2)],
            [CreateSequence("interact-a", 1)],
            [CreateSequence("speak-a", 1)]);

        Assert.NotNull(orchestrator.CurrentFrame);
        Assert.Equal(PetState.Idle, orchestrator.CurrentState);
    }

    [Fact]
    public void TriggerInteraction_AndAdvanceToEnd_ReturnsToIdle()
    {
        var idle = CreateSequence("idle-a", 2);
        var interact = CreateSequence("interact-a", 1);
        var speak = CreateSequence("speak-a", 1);

        var orchestrator = new PetAnimationOrchestrator(new Random(1));
        orchestrator.LoadSequences([idle], [interact], [speak]);

        var triggered = orchestrator.TryTriggerInteraction(DateTimeOffset.UtcNow);
        Assert.True(triggered);
        Assert.Equal(PetState.Interact, orchestrator.CurrentState);

        orchestrator.AdvanceFrames(1);

        Assert.Equal(PetState.Idle, orchestrator.CurrentState);
    }

    [Fact]
    public void TriggerSpeak_SwitchesToSpeakState()
    {
        var orchestrator = new PetAnimationOrchestrator(new Random(1));
        orchestrator.LoadSequences(
            [CreateSequence("idle-a", 2)],
            [CreateSequence("interact-a", 1)],
            [CreateSequence("speak-a", 2)]);

        var triggered = orchestrator.TryTriggerSpeak(DateTimeOffset.UtcNow);

        Assert.True(triggered);
        Assert.Equal(PetState.Speak, orchestrator.CurrentState);
    }

    private static AnimationSequence CreateSequence(string name, int frameCount)
    {
        var frames = new List<ImageSource>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            frames.Add(CreateSolidFrame((byte)(10 + i)));
        }

        return new AnimationSequence(name, frames);
    }

    private static BitmapSource CreateSolidFrame(byte shade)
    {
        var pixels = new byte[] { shade, shade, shade, 255 };
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        bitmap.Freeze();
        return bitmap;
    }
}
