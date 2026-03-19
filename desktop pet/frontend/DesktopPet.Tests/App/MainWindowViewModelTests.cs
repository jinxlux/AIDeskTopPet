using DesktopPet.App.ViewModels;
using System.Windows.Media.Imaging;

namespace DesktopPet.Tests.App;

public class MainWindowViewModelTests
{
    [Fact]
    public void ShowBubble_UpdatesMessageAndVisibility()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ShowBubble("汪汪");

        Assert.Equal("汪汪", viewModel.BubbleMessage);
        Assert.True(viewModel.IsBubbleVisible);
    }

    [Fact]
    public void SetCurrentFrame_UpdatesCurrentFrame()
    {
        var viewModel = new MainWindowViewModel();
        var frame = BitmapSource.Create(
            1,
            1,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            new byte[] { 255, 255, 255, 255 },
            4);

        viewModel.SetCurrentFrame(frame);

        Assert.Same(frame, viewModel.CurrentFrame);
    }
}
