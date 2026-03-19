using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using DesktopPet.App.Messaging;
using DesktopPet.App.Services;

namespace DesktopPet.Tests.App;

public class PetChatCoordinatorTests
{
    [Fact]
    public async Task SendAsync_PublishesBusyAndReplyMessages()
    {
        var runtimeManager = new FakeRuntimeManager();
        var session = new FakeChatSessionService("汪~你好");
        var tracker = new FakeRuntimeTracker();
        var messenger = new StrongReferenceMessenger();
        var events = new List<string>();

        messenger.Register<ChatBusyStateChangedMessage>(this, (_, message) => events.Add($"busy:{message.Value}"));
        messenger.Register<ChatReplyReceivedMessage>(this, (_, message) => events.Add($"reply:{message.Value}"));

        var coordinator = new PetChatCoordinator(runtimeManager, session, tracker, messenger);
        await coordinator.SendAsync("hello", useWebSearch: true, CancellationToken.None);

        Assert.Equal(1, runtimeManager.EnsureCalls);
        Assert.Equal(1, tracker.MarkInteractionCalls);
        Assert.Equal(new[] { "busy:True", "reply:汪~你好", "busy:False" }, events);
    }

    [Fact]
    public async Task SendAsync_PublishesError_WhenSessionFails()
    {
        var runtimeManager = new FakeRuntimeManager();
        var session = new FakeChatSessionService(null) { ThrowOnSend = true };
        var tracker = new FakeRuntimeTracker();
        var messenger = new StrongReferenceMessenger();
        var events = new List<string>();

        messenger.Register<ChatBusyStateChangedMessage>(this, (_, message) => events.Add($"busy:{message.Value}"));
        messenger.Register<ChatErrorMessage>(this, (_, message) => events.Add($"error:{message.Value}"));

        var coordinator = new PetChatCoordinator(runtimeManager, session, tracker, messenger);
        await coordinator.SendAsync("hello", useWebSearch: false, CancellationToken.None);

        Assert.Contains(events, x => x.StartsWith("error:"));
        Assert.Equal("busy:True", events.First());
        Assert.Equal("busy:False", events.Last());
    }

    private sealed class FakeRuntimeManager : IAiServiceRuntimeManager
    {
        public int EnsureCalls { get; private set; }
        public bool OwnedByDesktopPet => true;

        public Task EnsureServiceReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task EnsureRuntimeReadyAsync(CancellationToken cancellationToken)
        {
            EnsureCalls++;
            return Task.CompletedTask;
        }

        public Task StopRuntimeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopOwnedServiceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeChatSessionService : IPetChatSessionService
    {
        private readonly string? _reply;

        public FakeChatSessionService(string? reply)
        {
            _reply = reply;
        }

        public bool ThrowOnSend { get; set; }

        public Task<string> SendAsync(string userText, bool useWebSearch, CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
            {
                throw new System.InvalidOperationException("boom");
            }

            return Task.FromResult(_reply ?? string.Empty);
        }
    }

    private sealed class FakeRuntimeTracker : IRuntimeActivityTracker
    {
        public int MarkInteractionCalls { get; private set; }
        public int MarkStoppedCalls { get; private set; }

        public void MarkInteraction()
        {
            MarkInteractionCalls++;
        }

        public void MarkRuntimeStopped()
        {
            MarkStoppedCalls++;
        }
    }
}
