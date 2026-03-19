using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using DesktopPet.App.Messaging;
using DesktopPet.App.Services;
using DesktopPet.App.ViewModels;

namespace DesktopPet.Tests.App;

public class SettingWindowViewModelTests
{
    [Fact]
    public void DeleteSelectedAnimationCommand_Disables_ForBuiltInItem()
    {
        var catalog = new FakeCatalogService(
        [
            new AnimationAssetEntry { ActionType = "idle", Name = "idle-default", IsBuiltIn = true },
            new AnimationAssetEntry { ActionType = "idle", Name = "idle-custom", IsBuiltIn = false },
        ]);
        var vm = CreateViewModel(catalog);

        vm.SelectedAnimation = Assert.Single(vm.AnimationItems, x => x.Name == "idle-default");

        Assert.False(vm.DeleteSelectedAnimationCommand.CanExecute(null));
    }

    [Fact]
    public async Task ImportVideoCommand_Imports_AndPublishesMessage()
    {
        var catalog = new FakeCatalogService(
        [
            new AnimationAssetEntry { ActionType = "idle", Name = "idle-default", IsBuiltIn = true },
        ]);
        var import = new FakeImportService();
        var dialog = new FakeDialogService { PickedPath = @"D:\video\dog.mp4" };
        var messenger = new WeakReferenceMessenger();
        var messageReceived = false;
        messenger.Register<AssetsChangedMessage>(this, (_, _) => messageReceived = true);

        var vm = new SettingWindowViewModel(catalog, import, dialog, messenger);
        vm.BrowseVideoCommand.Execute(null);
        vm.SequenceName = "idle-custom-a";
        vm.FpsText = "12";

        await vm.ImportVideoCommand.ExecuteAsync(null);

        Assert.NotNull(import.LastRequest);
        Assert.Equal("idle", import.LastRequest!.ActionType);
        Assert.Equal("idle-custom-a", import.LastRequest.SequenceName);
        Assert.True(messageReceived);
        Assert.Equal("导入完成", vm.ImportStatusText);
    }

    [Fact]
    public async Task ImportVideoCommand_Rejects_BuiltInName()
    {
        var catalog = new FakeCatalogService(
        [
            new AnimationAssetEntry { ActionType = "idle", Name = "idle-default", IsBuiltIn = true },
        ]);
        var import = new FakeImportService();
        var dialog = new FakeDialogService { PickedPath = @"D:\video\dog.mp4" };
        var vm = CreateViewModel(catalog, import, dialog);

        vm.BrowseVideoCommand.Execute(null);
        vm.SequenceName = "idle-default";
        vm.FpsText = "12";

        await vm.ImportVideoCommand.ExecuteAsync(null);

        Assert.Null(import.LastRequest);
        Assert.Contains("导入失败", vm.ImportStatusText);
        Assert.Contains("默认动画名称不可复用", dialog.LastError ?? string.Empty);
    }

    private static SettingWindowViewModel CreateViewModel(
        FakeCatalogService catalog,
        FakeImportService? import = null,
        FakeDialogService? dialog = null)
    {
        return new SettingWindowViewModel(
            catalog,
            import ?? new FakeImportService(),
            dialog ?? new FakeDialogService(),
            new WeakReferenceMessenger());
    }

    private sealed class FakeCatalogService : IPetAnimationCatalogService
    {
        private readonly List<AnimationAssetEntry> _items;

        public FakeCatalogService(List<AnimationAssetEntry> items)
        {
            _items = items;
        }

        public AnimationAssetManifest EnsureManifest() => new() { Items = [.. _items] };

        public IReadOnlyList<AnimationAssetEntry> ListEntries() => [.. _items];

        public bool SequenceExists(string actionType, string sequenceName)
            => _items.Exists(x => x.ActionType == actionType && x.Name == sequenceName);

        public bool IsBuiltInSequence(string actionType, string sequenceName)
            => _items.Exists(x => x.ActionType == actionType && x.Name == sequenceName && x.IsBuiltIn);

        public bool DeleteCustomSequence(string actionType, string sequenceName)
        {
            var idx = _items.FindIndex(x => x.ActionType == actionType && x.Name == sequenceName && !x.IsBuiltIn);
            if (idx < 0)
            {
                return false;
            }

            _items.RemoveAt(idx);
            return true;
        }

        public void RestoreBuiltInDefaults()
        {
            _items.RemoveAll(x => !x.IsBuiltIn);
        }
    }

    private sealed class FakeImportService : IVideoAnimationImportService
    {
        public VideoAnimationImportRequest? LastRequest { get; private set; }

        public Task<string> ImportAsync(VideoAnimationImportRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(@"D:\out");
        }
    }

    private sealed class FakeDialogService : ISettingsDialogService
    {
        public string? PickedPath { get; set; }
        public string? LastError { get; private set; }

        public string? PickVideoFilePath() => PickedPath;

        public void ShowInfo(string message, string title = "提示")
        {
        }

        public void ShowError(string message, string title = "错误")
        {
            LastError = message;
        }

        public bool Confirm(string message, string title) => true;
    }
}
