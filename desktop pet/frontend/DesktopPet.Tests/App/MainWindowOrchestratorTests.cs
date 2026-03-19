using System;
using System.IO;
using DesktopPet.App.Services;
using DesktopPet.App.ViewModels;
using DesktopPet.Core.Services;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet.Tests.App;

public class MainWindowOrchestratorTests
{
    [Fact]
    public void TriggerInteraction_ShowsBubbleAndUpdatesFrame()
    {
        var viewModel = new MainWindowViewModel();
        var assetPath = CreateAssetsDirectory();
        var orchestrator = new MainWindowOrchestrator(
            viewModel,
            new PetAnimationOrchestrator(new Random(1)),
            new PetAssetService(assetPath),
            new RandomPhraseProvider(),
            new ThoughtPhraseProvider(),
            new Random(1));

        try
        {
            orchestrator.Initialize();
            orchestrator.TriggerInteraction();

            Assert.True(viewModel.IsBubbleVisible);
            Assert.False(string.IsNullOrWhiteSpace(viewModel.BubbleMessage));
            Assert.NotNull(viewModel.CurrentFrame);
        }
        finally
        {
            Directory.Delete(assetPath, recursive: true);
        }
    }

    [Fact]
    public void HandleChatReply_ShowsReplyBubble()
    {
        var viewModel = new MainWindowViewModel();
        var assetPath = CreateAssetsDirectory();
        var orchestrator = new MainWindowOrchestrator(
            viewModel,
            new PetAnimationOrchestrator(new Random(1)),
            new PetAssetService(assetPath),
            new RandomPhraseProvider(),
            new ThoughtPhraseProvider(),
            new Random(1));

        try
        {
            orchestrator.Initialize();
            orchestrator.HandleChatReply("你好呀");

            Assert.True(viewModel.IsBubbleVisible);
            Assert.Equal("你好呀", viewModel.BubbleMessage);
        }
        finally
        {
            Directory.Delete(assetPath, recursive: true);
        }
    }

    private static string CreateAssetsDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"main_window_assets_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        CreateSequenceDirectory(root, "idleRes", "idle-a", 2);
        CreateSequenceDirectory(root, "interactRes", "interact-a", 1);
        CreateSequenceDirectory(root, "speakRes", "speak-a", 1);
        return root;
    }

    private static void CreateSequenceDirectory(string root, string category, string name, int frameCount)
    {
        var dir = Path.Combine(root, category, name);
        Directory.CreateDirectory(dir);
        for (var i = 0; i < frameCount; i++)
        {
            var pixels = new byte[] { (byte)(10 + i), (byte)(20 + i), (byte)(30 + i), 255 };
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(dir, $"{i + 1:0000}.png"));
            encoder.Save(stream);
        }
    }
}
