using System.Threading.Tasks;
using DesktopPet.App.ViewModels;

namespace DesktopPet.Tests.App;

public class ChatInputWindowViewModelTests
{
    [Fact]
    public void CanSend_IsFalse_WhenInputIsEmpty()
    {
        var viewModel = new ChatInputWindowViewModel((_, _) => Task.CompletedTask, () => { });

        viewModel.InputText = "   ";

        Assert.False(viewModel.CanSend);
        Assert.False(viewModel.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task SendCommand_ClearsInput_AfterSuccessfulSend()
    {
        string? sentText = null;
        bool? sentUseWeb = null;
        var viewModel = new ChatInputWindowViewModel((text, useWeb) =>
        {
            sentText = text;
            sentUseWeb = useWeb;
            return Task.CompletedTask;
        }, () => { });

        viewModel.InputText = "你好";
        viewModel.IsWebSearchEnabled = true;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("你好", sentText);
        Assert.True(sentUseWeb);
        Assert.Equal(string.Empty, viewModel.InputText);
    }

    [Fact]
    public void SendCommand_CannotExecute_WhenBusy()
    {
        var viewModel = new ChatInputWindowViewModel((_, _) => Task.CompletedTask, () => { })
        {
            InputText = "hello",
            IsBusy = true,
        };

        Assert.False(viewModel.CanSend);
        Assert.False(viewModel.SendCommand.CanExecute(null));
    }
}
