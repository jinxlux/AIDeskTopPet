using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesktopPet.App.Services;

namespace DesktopPet.Tests.App;

public class PetChatSessionServiceTests
{
    [Fact]
    public async Task SendAsync_RememberKeyword_PersistsAndInjectsLongMemory()
    {
        var storageDir = CreateTempDirectory();
        try
        {
            var fakeChat = new FakeAiChatService
            {
                ReplyFactory = _ => "收到啦",
                JudgeReply = "NO",
            };

            var assetService = new PetAssetService(Path.Combine(storageDir, "Assets"));
            var service = new PetChatSessionService(fakeChat, assetService, storageDir);

            await service.SendAsync("记住你叫阿东", useWebSearch: false, CancellationToken.None);
            await service.SendAsync("你叫什么？", useWebSearch: false, CancellationToken.None);

            var secondTurnMessages = fakeChat.ChatAsPetInputs.Last();
            var memorySystemMessage = secondTurnMessages
                .FirstOrDefault(m => m.role == "system" && m.content.Contains("以下是你需要长期记住的信息"));

            Assert.NotNull(memorySystemMessage);
            Assert.Contains("记住你叫阿东", memorySystemMessage!.content);
            Assert.True(File.Exists(Path.Combine(storageDir, "long_memory.json")));
            Assert.True(File.Exists(Path.Combine(storageDir, "chat_history.json")));
        }
        finally
        {
            Directory.Delete(storageDir, recursive: true);
        }
    }

    [Fact]
    public async Task SendAsync_UsesAiJudge_WhenNoKeywordOrRule()
    {
        var storageDir = CreateTempDirectory();
        try
        {
            var fakeChat = new FakeAiChatService
            {
                ReplyFactory = _ => "好的",
                JudgeReply = "YES",
            };

            var assetService = new PetAssetService(Path.Combine(storageDir, "Assets"));
            var service = new PetChatSessionService(fakeChat, assetService, storageDir);
            await service.SendAsync("我今天在公司开会", useWebSearch: false, CancellationToken.None);

            Assert.Equal(1, fakeChat.ChatJudgeCallCount);
        }
        finally
        {
            Directory.Delete(storageDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pet_chat_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeAiChatService : IAiChatService
    {
        public List<IReadOnlyList<AiChatMessage>> ChatAsPetInputs { get; } = [];
        public int ChatJudgeCallCount { get; private set; }
        public string JudgeReply { get; set; } = "NO";
        public Func<IReadOnlyList<AiChatMessage>, string> ReplyFactory { get; set; } = _ => "ok";

        public Task<string> ChatAsync(IReadOnlyList<AiChatMessage> messages, CancellationToken cancellationToken)
        {
            ChatJudgeCallCount++;
            return Task.FromResult(JudgeReply);
        }

        public Task<string> ChatAsPetAsync(
            IReadOnlyList<AiChatMessage> messages,
            string userText,
            bool useWebSearch,
            CancellationToken cancellationToken)
        {
            ChatAsPetInputs.Add(messages);
            return Task.FromResult(ReplyFactory(messages));
        }
    }
}
